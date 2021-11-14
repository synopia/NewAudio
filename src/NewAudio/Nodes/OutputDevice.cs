using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class OutputDeviceParams : AudioParams
    {
        public AudioParam<OutputDeviceSelection> Device;
    }

    public class OutputDevice : AudioNode
    {
        public override string NodeName => "Output";
        private readonly IResourceHandle<DriverManager> _driverManager;

        public AudioFormat AudioFormat => ActualDeviceParams.AudioFormat;

        private readonly TransformBlock<AudioDataMessage, AudioDataMessage> _processor;
        private IDisposable _link;

        public VirtualOutput Device { get; private set; }

        private int _counter;
        private long _lag;
        public double LagMs { get; private set; }
        public ActualDeviceParams ActualDeviceParams { get; private set; }
        public DeviceParams DeviceParams { get; }
        public OutputDeviceParams Params { get; }
        
        public OutputDevice()
        {
            InitLogger<OutputDevice>();
            _driverManager = Factory.Instance.GetDriverManager();
            DeviceParams = AudioParams.Create<DeviceParams>();
            Params = AudioParams.Create<OutputDeviceParams>();
            Logger.Information("Output device created");

            _processor = new TransformBlock<AudioDataMessage, AudioDataMessage>(msg =>
            {
                var now = DateTime.Now.Ticks;
                var span = now - msg.Time.RealTime;
                var l = TimeSpan.FromTicks(span).TotalMilliseconds;

                _lag += span;
                _counter++;
                if (_counter <= 100)
                {
                    return msg;
                }

                LagMs = TimeSpan.FromTicks(_lag / 100).TotalMilliseconds;
                _lag = 0;
                _counter = 0;

                return msg;
            });
        }

        public AudioLink Update(AudioLink input, OutputDeviceSelection deviceSelection,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250, int bufferSize = 1)
        {
            Params.Device.Value = deviceSelection;
            DeviceParams.SamplingFrequency.Value = samplingFrequency;
            DeviceParams.DesiredLatency.Value = desiredLatency;
            DeviceParams.ChannelOffset.Value = channelOffset;
            DeviceParams.Channels.Value = channels;
            PlayParams.Update(input, Params.HasChanged, bufferSize);

            return base.Update(Params);
        }

        public override bool Play()
        {
            if (Params.Device.Value != null)
            {
                Device = _driverManager.Resource.GetOutputDevice(Params.Device.Value);
                if (Device != null)
                {
                    ActualDeviceParams = Device.Bind(DeviceParams);
                    _link = _processor.LinkTo(Device.TargetBlock);
                    Device.Start();
                    TargetBlock = _processor;
                    return true;
                }
            }

            return false;
        }

        public override void Stop()
        {
            _link?.Dispose();
            Device?.Dispose();
            Device = null;
            TargetBlock = null;
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
                    Device?.Dispose();
                    Device = null;
                    _driverManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}