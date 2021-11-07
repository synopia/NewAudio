using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Devices
{
    public class AsioDevice : BaseDevice
    {
        private AsioOut _asioOut;
        private readonly string _driverName;
        private readonly ILogger _logger;

        public AsioDevice(string name, string driverName)
        {
            Name = name;
            _driverName = driverName;
            IsInputDevice = true;
            IsOutputDevice = true;
            _logger = AudioService.Instance.Logger.ForContext<AsioDevice>();
        }

        public override Task<DeviceConfigResponse> Create(DeviceConfigRequest config)
        {
            _asioOut = new AsioOut(_driverName);
            var srs = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
                .Where(sr => _asioOut.IsSampleRateSupported((int)sr)).ToList();
            var configResponse = new DeviceConfigResponse
            {
                DriverRecordingChannels = _asioOut.DriverInputChannelCount,
                DriverPlayingChannels = _asioOut.DriverOutputChannelCount,
                SupportedSamplingFrequencies = srs
            };
            CancellationTokenSource = new CancellationTokenSource();
            
            if (config.IsPlaying && config.IsRecording)
            {
                AudioDataProvider = new AudioDataProvider(config.Playing.WaveFormat, config.Playing.Buffer)
                    {
                        CancellationToken = CancellationTokenSource.Token,
                        GenerateSilence = GenerateSilence
                    };

                _asioOut.InitRecordAndPlayback(AudioDataProvider, config.Recording.Channels,
                    config.Recording.WaveFormat.SampleRate);
                _asioOut.InputChannelOffset = config.Recording.ChannelOffset;
                _asioOut.ChannelOffset = config.Playing.ChannelOffset;
                _asioOut.AudioAvailable += OnAsioData;

                configResponse.RecordingChannels = _asioOut.NumberOfInputChannels;
                configResponse.PlayingChannels = _asioOut.NumberOfOutputChannels;
                configResponse.Latency = _asioOut.PlaybackLatency;
                // todo
                configResponse.PlayingWaveFormat = config.Playing.WaveFormat;
                configResponse.RecordingWaveFormat = config.Recording.WaveFormat;
                configResponse.FrameSize = _asioOut.FramesPerBuffer;
            }
            else if (config.IsRecording)
            {
                _asioOut.InitRecordAndPlayback(null, config.Recording.Channels, config.Recording.WaveFormat.SampleRate);
                _asioOut.AudioAvailable += OnAsioData;

                configResponse.RecordingChannels = _asioOut.NumberOfInputChannels;
                configResponse.Latency = _asioOut.PlaybackLatency;
                configResponse.FrameSize = _asioOut.FramesPerBuffer;
            }
            else if (config.IsPlaying)
            {
                AudioDataProvider = new AudioDataProvider(config.Playing.WaveFormat, config.Playing.Buffer)
                    {
                        CancellationToken = CancellationTokenSource.Token,
                        GenerateSilence = GenerateSilence
                    };

                _asioOut.Init(AudioDataProvider);

                configResponse.PlayingChannels = _asioOut.NumberOfOutputChannels;
                configResponse.Latency = _asioOut.PlaybackLatency;
                configResponse.FrameSize = _asioOut.FramesPerBuffer;
            }
            _asioOut.Play();
            return Task.FromResult(configResponse);
        }

        public override bool Free()
        {
            CancellationTokenSource?.Cancel();
            if (_asioOut != null && _asioOut.PlaybackState != PlaybackState.Stopped)
            {
                _asioOut.Stop();
            }
            _asioOut?.Dispose();
            return true;
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
            _logger.Information("{x}", evt.AsioSampleType);
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    CancellationTokenSource.Cancel();
                    _asioOut?.Stop();
                    _asioOut?.Dispose();
                    _asioOut = null;
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}