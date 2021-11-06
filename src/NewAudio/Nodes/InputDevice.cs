using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;

namespace NewAudio.Nodes
{
    public class InputDeviceConfig : AudioNodeConfig
    {
        public AudioParam<WaveInputDevice> Device;
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> DesiredLatency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> Channels;
    }

    public class InputDevice : AudioNode<InputDeviceConfig>
    {
        public readonly LifecycleStateMachine<InputDeviceConfig> StateMachine = new LifecycleStateMachine<InputDeviceConfig>();
        private AudioInputBlock _audioInputBlock;
        private readonly ILogger _logger;

        private IDevice _device;
        private AudioFormat _format;
        public WaveFormat WaveFormat => _format.WaveFormat;

        public InputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<InputDevice>();

            _logger.Information("Input device created");

            _format = new AudioFormat(48000, 512, 2);
            
        }

        protected override IEnumerable<IAudioParam> GetCreateParams()
        {
            return new IAudioParam[]{ Config.Device, Config.Channels, Config.ChannelOffset, Config.DesiredLatency, Config.SamplingFrequency};
        }

        public override async Task<bool> CreateResources(InputDeviceConfig config)
        {
            _device = (IDevice)Config.Device.Value?.Tag;
            if (_device == null)
            {
                return false;
            }
            _format = new AudioFormat((int)Config.SamplingFrequency.Value, 512, Config.Channels.Value);
            _audioInputBlock = new AudioInputBlock();
            await _audioInputBlock.CreateResources(new AudioInputBlockConfig()
            {
                AudioFormat = _format,
                NodeCount = 32
            });
            Output.SourceBlock = _audioInputBlock;
            Output.Format = _format;

            var req = new DeviceConfigRequest()
            {
                Recording = new DeviceConfig()
                {
                    Buffer = _audioInputBlock.Buffer,
                    Latency = Config.DesiredLatency.Value,
                    WaveFormat = WaveFormat,
                    Channels = Config.Channels.Value,
                    ChannelOffset = Config.ChannelOffset.Value
                }
            };
            var resp = await _device.CreateResources(req);
            _logger.Information("Device changed: {device} {resp}", _device, resp);
            return true;
        }

        public override Task<bool> FreeResources()
        {
            _device.FreeResources();
            return _audioInputBlock.FreeResources();
        }

        public override Task<bool> StartProcessing()
        {
            _device.StartProcessing();
            return _audioInputBlock.StartProcessing();
        }

        public override Task<bool> StopProcessing()
        {
            _device.StopProcessing();
            return _audioInputBlock.StopProcessing();
        }

        public override string DebugInfo()
        {
            return
                $"INPUT=[{Output?.SourceBlock?.Completion.Status}, {_device?.AudioDataProvider?.CancellationToken.IsCancellationRequested}]";
        }

        public AudioLink Update(WaveInputDevice device, SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
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
                    _audioInputBlock?.Dispose();
                    _device?.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}