﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    // ReSharper disable once ClassNeverInstantiated.Global
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class OutputDeviceInitParams : AudioNodeInitParams
    {
        public AudioParam<OutputDeviceSelection> Device;
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class OutputDevicePlayParams : AudioNodePlayParams
    {
    }

    public class OutputDevice : AudioNode<OutputDeviceInitParams, OutputDevicePlayParams>
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
        public DeviceParams DeviceParams { get; private set; }
        
        public OutputDevice()
        {
            InitLogger<OutputDevice>();
            _driverManager = Factory.Instance.GetDriverManager();
            DeviceParams = AudioParams.Create<DeviceParams>();
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
            int channelOffset = 0, int channels = 2, int desiredLatency = 250, int bufferSize = 4)
        {
            PlayParams.BufferSize.Value = bufferSize;
            PlayParams.Input.Value = input;
            InitParams.Device.Value = deviceSelection;
            DeviceParams.SamplingFrequency.Value = samplingFrequency;
            DeviceParams.DesiredLatency.Value = desiredLatency;
            DeviceParams.ChannelOffset.Value = channelOffset;
            DeviceParams.Channels.Value = channels;
            ActualDeviceParams?.Update();

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

        public override Task<bool> Init()
        {
            if (InitParams.Device.Value == null)
            {
                Logger.Error("No output device selected. Should not happen!");
            }

            Device = _driverManager.Resource.GetOutputDevice(InitParams.Device.Value);
            if (Device == null)
            {
                return Task.FromResult(false);
            }

            ActualDeviceParams = Device.Bind(DeviceParams);
            
            /*
            AudioFormat = new AudioFormat((int)InitParams.SamplingFrequency.Value, 512, InitParams.Channels.Value);
            var req = new DeviceConfigRequest
            {
                Latency = InitParams.DesiredLatency.Value,
                AudioFormat = AudioFormat,
                Channels = InitParams.Channels.Value,
                ChannelOffset = InitParams.ChannelOffset.Value
            };
            var res = await Device.CreateOutput(req);
            if (res == null)
            {
                return false;
            }

            var resp = res.Item1;
            Logger.Information(
                "Output device changed: {Device} Channels={Channels}, Driver Channels={Driver}, Latency={Latency}, Frame size={FrameSize}",
                Device, resp.Channels, resp.DriverChannels, resp.Latency, resp.FrameSize);

            AudioFormat = resp.AudioFormat;
            */
            // var output = res.Item2;
            // _link = _processor.LinkTo(output);
            _link = _processor.LinkTo(Device.TargetBlock);

            return Task.FromResult(true);
        }

        public override Task<bool> Free()
        {
            _link.Dispose();
            Device.Dispose();
            return Task.FromResult(true);
        }

        public override bool Play()
        {
            Device.Start();
            TargetBlock = _processor;
            return true;
        }

        public override bool Stop()
        {
            TargetBlock = null;
            Device.Stop();
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