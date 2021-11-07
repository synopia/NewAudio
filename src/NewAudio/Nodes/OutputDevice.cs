﻿using System;
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
    public class OutputDeviceCreateParams : AudioNodeCreateParams
    {
        public AudioParam<WaveOutputDevice> Device;
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> DesiredLatency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> Channels;
    }
    public class OutputDeviceUpdateParams : AudioNodeUpdateParams
    {
    }

    public class OutputDevice : AudioNode<OutputDeviceCreateParams, OutputDeviceUpdateParams>
    {
        private readonly ILogger _logger;
        private AudioOutputBlock _audioOutputBlock;
        
        private IDevice _device;
        private AudioFormat _format;
        public WaveFormat WaveFormat => _format.WaveFormat;

        private readonly TransformBlock<AudioDataMessage, AudioDataMessage> _processor;
        private IDisposable _link;
        private IDisposable _inputBufferLink;
        

        private int _counter;
        private long _lag;
        public double LagMs { get; private set; }
        
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
                    LagMs = TimeSpan.FromTicks(_lag / 100).TotalMilliseconds;
                    _lag = 0;
                    _counter = 0;
                }

                return msg;
            });
        }

        public AudioLink Update(AudioLink input, WaveOutputDevice device,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            UpdateParams.Input.Value = input;
            CreateParams.Device.Value = device;
            CreateParams.SamplingFrequency.Value = samplingFrequency;
            CreateParams.DesiredLatency.Value = desiredLatency;
            CreateParams.ChannelOffset.Value = channelOffset;
            CreateParams.Channels.Value = channels;

            return Update();
        }
        public override bool IsCreateValid()
        {
            return CreateParams.Device.Value != null;
        }

        public override bool IsUpdateValid()
        {
            return UpdateParams.Input.Value != null;
        }

        public override async Task<bool> Create()
        {
            _device = (IDevice)CreateParams.Device.Value.Tag;
            _format = new AudioFormat((int)CreateParams.SamplingFrequency.Value, 512, CreateParams.Channels.Value);
            _audioOutputBlock = new AudioOutputBlock();
            _audioOutputBlock.Create(new AudioOutputBlockConfig()
            {
                AudioFormat = _format,
                NodeCount = 32
            });
            _link = _processor.LinkTo(_audioOutputBlock);

            var req = new DeviceConfigRequest()
            {
                Playing = new DeviceConfig()
                {
                    Buffer = _audioOutputBlock.Buffer,
                    Latency = CreateParams.DesiredLatency.Value,
                    WaveFormat = WaveFormat,
                    Channels = CreateParams.Channels.Value,
                    ChannelOffset = CreateParams.ChannelOffset.Value
                }
            };
            var resp = await _device.Create(req);
            _logger.Information("Output device changed: {device} Channels={channels}, Driver Channels={driver}, Latency={latency}, Frame size={frameSize}", _device, resp.PlayingChannels, resp.DriverPlayingChannels, resp.Latency, resp.FrameSize);
            return true;
        }

        public override async Task<bool> Free()
        {
            _link.Dispose();
            _device.Free();
            await _audioOutputBlock.Free();
            return true;
        }

        public override bool Play()
        {
            _device.Start();
            _inputBufferLink = InputBufferBlock.LinkTo(_processor);
            return true;
        }

        public override bool Stop()
        {
            _device.Stop();
            _inputBufferLink.Dispose();
            _inputBufferLink = null;
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
                    _inputBufferLink?.Dispose();
                    _audioOutputBlock?.Dispose();
                    _device = null;
                    _inputBufferLink = null;
                    _audioOutputBlock = null;
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}