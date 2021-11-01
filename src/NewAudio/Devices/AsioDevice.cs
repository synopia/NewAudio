using System.Threading;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Devices
{
    public class AsioDevice: BaseDevice
    {
        private string _driverName;
        private AsioOut _asioOut;
        private bool _isRecording;
        private bool _isPlaying;
        private bool _isInitialized;
        
        public AsioDevice(string name, string driverName)
        {
            Name = name;
            _driverName = driverName;
            IsInputDevice = true;
            IsOutputDevice = true;
        }

        private CircularBuffer _buffer;
        private WaveFormat _waveFormat;
        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop)
        {
            _waveFormat = waveFormat;
            _buffer = buffer;
            PlayPauseStop = playPauseStop;
            _isPlaying = true;
            Logger.Information("Starting Asio Playback, T={T}, CH={CH}, ENC={ENC}", waveFormat.SampleRate, waveFormat.Channels, waveFormat.Encoding);
            DoInit();
            // _logger.Info($"DriverInputChannelCount {asioOut.DriverInputChannelCount}");
            // _logger.Info($"DriverOutputChannelCount {asioOut.DriverOutputChannelCount}");
            // _logger.Info($"PlaybackLatency {asioOut.PlaybackLatency}");
            // _logger.Info($"NumberOfInputChannels {asioOut.NumberOfInputChannels}");
            // _logger.Info($"NumberOfOutputChannels {asioOut.NumberOfOutputChannels}");
            // _logger.Info($"ChannelOffset {asioOut.ChannelOffset}");
            // _logger.Info($"InputChannelOffset {asioOut.InputChannelOffset}");
            // _logger.Info($"FramesPerBuffer {asioOut.FramesPerBuffer}");
            // _logger.Info($"{asioOut.IsSampleRateSupported(48000)}");
            // _logger.Info($"{asioOut.IsSampleRateSupported(44100)}");
        }

        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop)
        {
            _isRecording = true;
        }

        private void OnAsioData(object sender, AsioAudioAvailableEventArgs evt)
        {
            // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            // if (RecordingBuffer != null)
            // {
                // Buffers.WriteAll(RecordingBuffer, evt.Buffer, evt.BytesRecorded, Token);
            // }
        }
        private void DoInit()
        {
            AudioDataProvider = new AudioDataProvider(_waveFormat, _buffer, PlayPauseStop);
            _asioOut = new AsioOut(_driverName);
            if (_isRecording )
            {
                _asioOut.InitRecordAndPlayback(AudioDataProvider, 2, RecordingWaveFormat.SampleRate);
                _asioOut.AudioAvailable += OnAsioData;
            } else if (_isPlaying)
            {
                _asioOut.Init(AudioDataProvider);
            }

            _isInitialized = true;
        }

        public override void Record()
        {
            if (!_isInitialized)
            {
                // DoInit();
            }
            _asioOut?.Play();
        }

        public override void Play()
        {
            if (!_isInitialized)
            {
                // DoInit();
            }
            _asioOut?.Play();
        }

        public override void Stop()
        {
            _asioOut?.Stop();
            _asioOut?.Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
            _asioOut?.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}