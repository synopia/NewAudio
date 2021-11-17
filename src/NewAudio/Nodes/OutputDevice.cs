using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{


    public abstract class OutputNode : AudioNode
    {
        private int _counter;
        private long _lag;
        public double LagMs { get; private set; }

        private ulong _lastClip;
        private bool _clipDetectionEnabled;
        private float _clipThreshold;
        public bool IsClipDetectionEnabled => _clipDetectionEnabled;

        public ulong LastClip
        {
            get
            {
                var clip = _lastClip;
                _lastClip = 0;
                return clip;
            }
        }

        public abstract int OutputSampleRate { get; }
        public abstract int OutputFramesPerBlock { get; }

        public abstract void UpdateConfig(DeviceConfig config);
        
        protected OutputNode(AudioNodeConfig config) : base(config)
        {
            
        }

        public override void Connect(AudioNode output)
        {
            throw new InvalidOperationException("Not supported!");
        }

        protected bool CheckNotClipping()
        {
            if (_clipDetectionEnabled)
            {
                var recordedFrame = 0;
                if (AudioMath.ThresholdBuffer(_internalBuffer, _clipThreshold, out recordedFrame))
                {
                    _lastClip = Graph.LastProcessedFrame + (ulong)recordedFrame;
                    return true;
                }
            }

            return false;
        }

        public void EnableClipDetection(bool enable = true, float threshold = 2)
        {
            // todo lock
            _clipDetectionEnabled = enable;
            _clipThreshold = threshold;
        }

        public abstract int FillBuffer(byte[] buffer, int offset, int count);

    }
    // ReSharper disable once ClassNeverInstantiated.Global
    public class OutputDeviceParams : AudioParams
    {
        public AudioParam<OutputDeviceSelection> Device;
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> NumberOfChannels;
        public AudioParam<int> FramesPerBlock;
        public AudioParam<int> DesiredLatency;
    }
    public class OutputDevice: AudioNode {
        public override string NodeName => "OutputDevice";
        private readonly IResourceHandle<DriverManager> _driverManager;

        public DeviceConfig DeviceConfig { get; private set; }
        public DeviceConfigRequest DeviceConfigRequest { get; }
        public OutputDeviceParams Params { get; }
        public VirtualOutput Device { get; private set; }
        
        public OutputDevice(AudioNodeConfig config) : base(config)
        {
            InitLogger<OutputNode>();
            _driverManager = Factory.GetDriverManager();
            Params = AudioParams.Create<OutputDeviceParams>();
            Logger.Information("Output device created");
        }

        
        public AudioLink Update(AudioLink input, OutputDeviceSelection deviceSelection,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250, int framesPerBlock = 1)
        {
            Params.Device.Value = deviceSelection;
            Params.SamplingFrequency.Value = samplingFrequency;
            Params.DesiredLatency.Value = desiredLatency;
            Params.ChannelOffset.Value = channelOffset;
            Params.NumberOfChannels.Value = channels;
            Params.FramesPerBlock.Value = framesPerBlock;
            
            if (Params.HasChanged)
            {
                StopDevice();
                StartDevice();
                Params.Commit();
            }

            return Output;
        }

        public void StartDevice()
        {
            if (Params.Device.Value == null || Params.NumberOfChannels.Value<=0 || Params.FramesPerBlock.Value<=0)
            {
                return;
            }

            Device = _driverManager.Resource.GetOutputDevice(Params.Device.Value, new DeviceConfigRequest()
            {
                Channels = Params.NumberOfChannels.Value,
                ChannelOffset = Params.ChannelOffset.Value,
                DesiredLatency = Params.DesiredLatency.Value,
                SamplingFrequency = Params.SamplingFrequency.Value,
                FramesPerBlock = Params.FramesPerBlock.Value
            });
            
            if (Device == null)
            {
                return;
            }

            Connect(Device);
        }

        public void StopDevice()
        {
            Device?.Dispose();
            Device = null;
        }
        
        public override string DebugInfo()
        {
            return $"Output device:[{base.DebugInfo()}]";
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    StopDevice();
                    _driverManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}