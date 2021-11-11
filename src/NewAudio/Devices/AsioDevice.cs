﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Devices
{
    public class AsioDevice : BaseDevice
    {
        private AsioOut _asioOut;
        private readonly string _driverName;

        public AsioDevice(string name, string driverName)
        {
            Name = name;
            InitLogger<AsioDevice>();
            _driverName = driverName;
            IsInputDevice = true;
            IsOutputDevice = true;
        }

        protected override DeviceConfigResponse PrepareRecording(DeviceConfigRequest request)
        {
            if (_asioOut == null)
            {
                _asioOut = new AsioOut(_driverName);
            }
            return base.PrepareRecording(request);
        }

        protected override DeviceConfigResponse PreparePlaying(DeviceConfigRequest request)
        {
            if (_asioOut == null)
            {
                _asioOut = new AsioOut(_driverName);
            }

            return new DeviceConfigResponse()
            {
                Channels = 2,
                AudioFormat = request.AudioFormat,
                ChannelOffset = 0,
                Latency = 0,
                DriverChannels = _asioOut.DriverOutputChannelCount,
                FrameSize = request.AudioFormat.BufferSize,
                SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
                    .Where(sr => _asioOut.IsSampleRateSupported((int)sr)).ToList()
            };
        }

        protected override Task<bool> Init()
        {
            if (IsPlaying && IsRecording)
            {
                _asioOut.InitRecordAndPlayback(AudioDataProvider, RecordingConfig.Channels,
                    RecordingConfig.AudioFormat.SampleRate);
                _asioOut.InputChannelOffset = RecordingConfig.ChannelOffset;
                _asioOut.ChannelOffset = PlayingConfig.ChannelOffset;
                _asioOut.AudioAvailable += OnAsioData;
                RecordingConfig.Channels = _asioOut.NumberOfInputChannels;
                PlayingConfig.Channels = _asioOut.NumberOfOutputChannels;
                PlayingConfig.Latency = _asioOut.PlaybackLatency;
                RecordingConfig.Latency = _asioOut.PlaybackLatency;
                // todo
                PlayingConfig.FrameSize = _asioOut.FramesPerBuffer;
                RecordingConfig.FrameSize = _asioOut.FramesPerBuffer;
            } else if (IsRecording)
            {
                _asioOut.InitRecordAndPlayback(null, RecordingConfig.Channels, RecordingConfig.AudioFormat.SampleRate);
                _asioOut.AudioAvailable += OnAsioData;
                RecordingConfig.Channels = _asioOut.NumberOfInputChannels;
                RecordingConfig.Latency = _asioOut.PlaybackLatency;
                RecordingConfig.FrameSize = _asioOut.FramesPerBuffer;
            } else if (IsPlaying)
            {
                _asioOut.Init(AudioDataProvider);
                PlayingConfig.Channels = _asioOut.NumberOfOutputChannels;
                PlayingConfig.Latency = _asioOut.PlaybackLatency;
                PlayingConfig.FrameSize = _asioOut.FramesPerBuffer;
            }
            _asioOut.Play();
            return Task.FromResult(_asioOut.PlaybackState==PlaybackState.Playing);
        }

        public override bool Start()
        {
            GenerateSilence = false;
            return true;
        }

        public override bool Stop()
        {
            GenerateSilence = true;
            return true;
        }

        private void OnAsioData(object sender, AsioAudioAvailableEventArgs evt)
        {
            // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            // if (RecordingBuffer != null)
            // {
            // Buffers.WriteAll(RecordingBuffer, evt.Buffer, evt.BytesRecorded, Token);
            // }
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    CancellationTokenSource?.Cancel();
                    if (_asioOut != null)
                    {
                        _asioOut.Stop();
                        _asioOut.AudioAvailable -= OnAsioData;
                        _asioOut?.Dispose();
                    }

                    _asioOut = null;
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

        public override string DebugInfo()
        {
            return $"[{this}, {_asioOut?.PlaybackState}, {base.DebugInfo()}]";
        }
    }
}