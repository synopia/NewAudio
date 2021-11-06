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
        private readonly ILogger _logger;
        private AudioInputBlock _audioInputBlock;

        private IDevice _device;
        private AudioFormat _format;
        public WaveFormat WaveFormat => _format.WaveFormat;

        public InputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<InputDevice>();
            _logger.Information("Input device created");
            _format = new AudioFormat(48000, 512, 2);
        }
        
        public AudioLink Update(WaveInputDevice device, SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            Config.Device.Value = device;
            Config.SamplingFrequency.Value = samplingFrequency;
            Config.DesiredLatency.Value = desiredLatency;
            Config.ChannelOffset.Value = channelOffset;
            Config.Channels.Value = channels;

            return Update();
        }

        public override bool IsInputValid(InputDeviceConfig next)
        {
            return next.Device.Value!=null;
        }

        public override async Task<bool> Create(InputDeviceConfig config)
        {
            _device = (IDevice)Config.Device.Value.Tag;
            _format = new AudioFormat((int)Config.SamplingFrequency.Value, 512, Config.Channels.Value);
            _audioInputBlock = new AudioInputBlock();
            _audioInputBlock.Create(new AudioInputBlockConfig()
            {
                AudioFormat = _format,
                NodeCount = 32
            });
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
            var resp = await _device.Create(req);
            _logger.Information("Input device changed: {device} Channels={channels}, Driver Channels={driver}, Latency={latency}, Frame size={frameSize}", _device, resp.RecordingChannels, resp.DriverRecordingChannels, resp.Latency, resp.FrameSize);
            return true;
        }

        public override Task<bool> Free()
        {
            _device.Free();
            return _audioInputBlock.Free();
        }

        public override bool Start()
        {
            Output.SourceBlock = _audioInputBlock;
            _device.Start();
            _audioInputBlock.Start();
            return true;
        }

        public override bool Stop()
        {
            Output.SourceBlock = null;
            _audioInputBlock.Stop();
            _device.Stop();
            return true;
        }

        public override string DebugInfo()
        {
            return null;
        }


        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _device?.Dispose();
                    _audioInputBlock?.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}