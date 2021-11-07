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
    public class InputDeviceCreateParams : AudioNodeCreateParams
    {
        public AudioParam<WaveInputDevice> Device;
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> DesiredLatency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> Channels;
    }
    public class InputDeviceUpdateParams : AudioNodeUpdateParams
    {
    }

    public class InputDevice : AudioNode<InputDeviceCreateParams, InputDeviceUpdateParams>
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
            CreateParams.Device.Value = device;
            CreateParams.SamplingFrequency.Value = samplingFrequency;
            CreateParams.DesiredLatency.Value = desiredLatency;
            CreateParams.ChannelOffset.Value = channelOffset;
            CreateParams.Channels.Value = channels;

            return Update();
        }

        public override bool IsCreateValid()
        {
            return CreateParams.Device.Value!=null;
        }

        public override async Task<bool> Create()
        {
            _device = (IDevice)CreateParams.Device.Value.Tag;
            _format = new AudioFormat((int)CreateParams.SamplingFrequency.Value, 512, CreateParams.Channels.Value);
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
                    Latency = CreateParams.DesiredLatency.Value,
                    WaveFormat = WaveFormat,
                    Channels = CreateParams.Channels.Value,
                    ChannelOffset = CreateParams.ChannelOffset.Value
                }
            };
            var resp = await _device.Create(req);
            _logger.Information("Input device changed: {device} Channels={channels}, Driver Channels={driver}, Latency={latency}, Frame size={frameSize}", _device, resp.RecordingChannels, resp.DriverRecordingChannels, resp.Latency, resp.FrameSize);
            _device.Start();
            return true;
        }

        public override async Task<bool> Free()
        {
            _device.Stop();
            _device.Free();
            await _audioInputBlock.Free();
            return true;
        }

        public override bool Play()
        {
            Output.SourceBlock = _audioInputBlock;
            _device.Start();
   
            return true;
        }

        public override bool Stop()
        {
            Output.SourceBlock = null;
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
                    _device = null;
                    _audioInputBlock = null;
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}