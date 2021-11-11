using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;
using VL.Lib.Basics.Resources;

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
        private IResourceHandle<DriverManager> _driverManager;

        private IResourceHandle<IDevice> _device;
        private AudioFormat _format;

        public InputDevice()
        {
            InitLogger<InputDevice>();
            _driverManager = VLApi.Instance.GetDriverManager();
            Logger.Information("Input device created");
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
            _device = _driverManager.Resource.GetInputDevice(InitParams.Device.Value);

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
            var res = await _device.Resource.CreateInput(req);
            if (res == null)
            {
                return false;
            }
            var resp = res.Item1;
            Logger.Information("Input device changed: {device} Channels={channels}, Driver Channels={driver}, Latency={latency}, Frame size={frameSize}", _device?.Resource, resp.Channels, resp.DriverChannels, resp.Latency, resp.FrameSize);

            _format = resp.AudioFormat;
            Output.SourceBlock = res.Item2;
            Output.Format = _format;

            return true;
        }

        public override Task<bool> Free()
        {
            _device?.Dispose();
            Output.SourceBlock = null;
            return Task.FromResult(true);
        }

        public override bool Play()
        {
            _device.Resource.Start();
   
            return true;
        }

        public override bool Stop()
        {
            _device.Resource.Stop();
    
            return true;
        }

        public override string DebugInfo()
        {
            return $"Input device:[{base.DebugInfo()}]";
        }


        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _device?.Dispose();
                    _device = null;
                    _driverManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}