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
    public class InputDeviceInitParams : AudioNodeInitParams
    {
        public AudioParam<WaveInputDevice> Device;
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> DesiredLatency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> Channels;
    }
    public class InputDevicePlayParams : AudioNodePlayParams
    {
    }

    public class InputDevice : AudioNode<InputDeviceInitParams, InputDevicePlayParams>
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
        
        public AudioLink Update(WaveInputDevice device, SamplingFrequency samplingFrequency = SamplingFrequency.Hz48000,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            InitParams.Device.Value = device;
            InitParams.SamplingFrequency.Value = samplingFrequency;
            InitParams.DesiredLatency.Value = desiredLatency;
            InitParams.ChannelOffset.Value = channelOffset;
            InitParams.Channels.Value = channels;

            return base.Update();
        }

        public override bool IsInitValid()
        {
            return InitParams.Device.Value!=null;
        }

        public override async Task<bool> Init()
        {
            _device = (IDevice)InitParams.Device.Value.Tag;
            _format = new AudioFormat((int)InitParams.SamplingFrequency.Value, 512, InitParams.Channels.Value);
            _audioInputBlock = new AudioInputBlock();
            _audioInputBlock.Create(Output.TargetBlock, _format, 32);
            Output.Format = _format;

            var req = new DeviceConfigRequest()
            {
                Recording = new DeviceConfig()
                {
                    Buffer = _audioInputBlock.Buffer,
                    Latency = InitParams.DesiredLatency.Value,
                    WaveFormat = WaveFormat,
                    Channels = InitParams.Channels.Value,
                    ChannelOffset = InitParams.ChannelOffset.Value
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
            Output.Play();
            _device.Start();
   
            return true;
        }

        public override bool Stop()
        {
            Output.Stop();
            _device.Stop();
    
            return true;
        }

        public override string DebugInfo()
        {
            return $"Input device:[ {base.DebugInfo()} ]";
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