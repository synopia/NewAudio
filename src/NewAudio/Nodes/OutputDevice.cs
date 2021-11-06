using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;

namespace NewAudio.Nodes
{
    public class OutputDeviceConfig : AudioNodeConfig
    {
        public AudioParam<WaveOutputDevice> Device;
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> DesiredLatency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> Channels;
    }

    public class OutputDevice : AudioNode<OutputDeviceConfig>
    {
        private readonly ILogger _logger;
        private int _counter;
        private IDevice _device;
        private AudioFormat _format;
        private long _lag;
        private double _lagMs;
        private AudioOutputBlock _audioOutputBlock;
        private TransformBlock<AudioDataMessage, AudioDataMessage> _processor;
        public WaveFormat WaveFormat => _format.WaveFormat;

        public OutputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<OutputDevice>();
            _logger.Information("Output device created");
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
                    _lagMs = TimeSpan.FromTicks(_lag / 100).TotalMilliseconds;
                    _lag = 0;
                    _counter = 0;
                }

                return msg;
            });
        }
        protected override IEnumerable<IAudioParam> GetCreateParams()
        {
            return new IAudioParam[]{ Config.Device, Config.Channels, Config.ChannelOffset, Config.DesiredLatency, Config.SamplingFrequency};

        }

        protected override void OnConnect(AudioLink input)
        {
            _logger.Information("New connection to output device");
            AddLink(input.SourceBlock.LinkTo(_processor));
        }

        public override async Task<bool> CreateResources(OutputDeviceConfig config)
        {
            _device = (IDevice)Config.Device.Value?.Tag;
            if (_device == null)
            {
                return false;
            }
            _format = new AudioFormat((int)Config.SamplingFrequency.Value, 512, Config.Channels.Value);
            _audioOutputBlock = new AudioOutputBlock();
            await _audioOutputBlock.CreateResources(new AudioOutputBlockConfig()
            {
                AudioFormat = _format,
                NodeCount = 32
            });
            _processor.LinkTo(_audioOutputBlock);

            var req = new DeviceConfigRequest()
            {
                Playing = new DeviceConfig()
                {
                    Buffer = _audioOutputBlock.Buffer,
                    Latency = Config.DesiredLatency.Value,
                    WaveFormat = WaveFormat,
                    Channels = Config.Channels.Value,
                    ChannelOffset = Config.ChannelOffset.Value
                }
            };
            if (config.Input != null)
            {
                AddLink(Config.Input.Value?.SourceBlock?.LinkTo(_processor));
            }
            var resp = await _device.CreateResources(req);
            _logger.Information("Device changed: {device} {resp}", _device, resp);
            return true;
        }

        public override Task<bool> FreeResources()
        {
            DisposeLinks();
            _device.FreeResources();
            return _audioOutputBlock.FreeResources();
        }

        public override Task<bool> StartProcessing()
        {
            _device.StartProcessing();
            return _audioOutputBlock.StartProcessing();
        }

        public override Task<bool> StopProcessing()
        {
            _device.StopProcessing();
            return _audioOutputBlock.StopProcessing();
        }

        public override string DebugInfo()
        {
            return
                $"OUTPUT=[{_processor?.Completion.Status}, {_device?.AudioDataProvider?.CancellationToken.IsCancellationRequested}]";
        }

        public AudioLink Update(AudioLink input, WaveOutputDevice device,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            Config.Input.Value = input;
            Config.Device.Value = device;
            Config.SamplingFrequency.Value = samplingFrequency;
            Config.DesiredLatency.Value = desiredLatency;
            Config.ChannelOffset.Value = channelOffset;
            Config.Channels.Value = channels;

            return Update().GetAwaiter().GetResult();
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _audioOutputBlock?.Dispose();
                    _device?.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}