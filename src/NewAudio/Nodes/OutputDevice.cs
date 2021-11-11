using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public class OutputDeviceInitParams : AudioNodeInitParams
    {
        public AudioParam<WaveOutputDevice> Device;
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> DesiredLatency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> Channels;
    }

    public class OutputDevicePlayParams : AudioNodePlayParams
    {
    }

    public class OutputDevice : AudioNode<OutputDeviceInitParams, OutputDevicePlayParams>
    {
        public override string NodeName => "Output";
        private AudioOutputBlock _audioOutputBlock;
        private IResourceHandle<DriverManager> _driverManager;

        private IResourceHandle<IDevice> _device;
        private AudioFormat _format;
        public WaveFormat WaveFormat => _format.WaveFormat;

        private readonly TransformBlock<AudioDataMessage, AudioDataMessage> _processor;
        private IDisposable _link;


        private int _counter;
        private long _lag;
        public double LagMs { get; private set; }

        public OutputDevice()
        {
            InitLogger<OutputDevice>();
            _driverManager = VLApi.Instance.GetDriverManager();
            Logger.Information("Output device created");
            _format = new AudioFormat(48000, 512, 2);

            _processor = new TransformBlock<AudioDataMessage, AudioDataMessage>(msg =>
            {
                var now = DateTime.Now.Ticks;
                var span = now - msg.Time.RealTime;
                var l = TimeSpan.FromTicks(span).TotalMilliseconds;

                _lag += span;
                _counter++;
                if (_counter > 100)
                {
                    LagMs = TimeSpan.FromTicks(_lag / 100).TotalMilliseconds;
                    _lag = 0;
                    _counter = 0;
                }

                return msg;
            });
        }

        public AudioLink Update(AudioLink input, WaveOutputDevice device,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250, int bufferSize=4)
        {
            PlayParams.BufferSize.Value = bufferSize;
            PlayParams.Input.Value = input;
            InitParams.Device.Value = device;
            InitParams.SamplingFrequency.Value = samplingFrequency;
            InitParams.DesiredLatency.Value = desiredLatency;
            InitParams.ChannelOffset.Value = channelOffset;
            InitParams.Channels.Value = channels;

            return base.Update();
        }

        public override bool IsInitValid()
        {
            return InitParams.Device.Value != null;
        }

        public override bool IsPlayValid()
        {
            return PlayParams.Input.Value != null;
        }

        public override async Task<bool> Init()
        {
            _device = _driverManager.Resource.GetOutputDevice(InitParams.Device.Value);
            if (_device.Resource == null)
            {
                return false;
            }
            var req = new DeviceConfigRequest()
            {
                Latency = InitParams.DesiredLatency.Value,
                AudioFormat = new AudioFormat((int)InitParams.SamplingFrequency.Value, 512, InitParams.Channels.Value),
                Channels = InitParams.Channels.Value,
                ChannelOffset = InitParams.ChannelOffset.Value
            };
            var res = await _device.Resource.CreateOutput(req);
            if (res == null)
            {
                return false;
            }
            var resp = res.Item1;
            Logger.Information(
                "Output device changed: {device} Channels={channels}, Driver Channels={driver}, Latency={latency}, Frame size={frameSize}",
                _device, resp.Channels, resp.DriverChannels, resp.Latency, resp.FrameSize);
            
            _format = resp.AudioFormat;
            var output = res.Item2;
            // _audioOutputBlock = new AudioOutputBlock();
            // _audioOutputBlock.Create(_format, 2);
            _link = _processor.LinkTo(output);

            return true;
        }

        public override  Task<bool> Free()
        {
            _link.Dispose();
            _device.Dispose();
            // await _audioOutputBlock.Free();
            return Task.FromResult(true);
        }

        public override bool Play()
        {
            _device.Resource.Start();
            TargetBlock = _processor;
            return true;
        }

        public override bool Stop()
        {
            TargetBlock = null;
            _device.Resource.Stop();
            return true;
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
                    _device?.Dispose();
                    // _audioOutputBlock?.Dispose();
                    _device = null;
                    _audioOutputBlock = null;
                    _driverManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}