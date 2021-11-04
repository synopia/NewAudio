using System.Threading;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Devices
{
    public class AsioDevice : BaseDevice
    {
        private AsioOut _asioOut;
        private CircularBuffer _buffer;
        private readonly string _driverName;
        private bool _isInitialized;
        private bool _isPlaying;
        private bool _isRecording;
        private readonly ILogger _logger;
        private WaveFormat _waveFormat;

        public AsioDevice(string name, string driverName)
        {
            Name = name;
            _driverName = driverName;
            IsInputDevice = true;
            IsOutputDevice = true;
            _logger = AudioService.Instance.Logger.ForContext<AsioDevice>();
        }

        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
            _waveFormat = waveFormat;
            _buffer = buffer;
            _isPlaying = true;
            _logger.Information("Starting Asio Playback, T={T}, CH={CH}, ENC={ENC}", waveFormat.SampleRate,
                waveFormat.Channels, waveFormat.Encoding);


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

        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
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
            
            _asioOut = new AsioOut(_driverName);

            if (_isRecording && _isPlaying )
            {
                AudioDataProvider = new AudioDataProvider(_waveFormat, _buffer);
                _asioOut.InitRecordAndPlayback(AudioDataProvider, 2, RecordingWaveFormat.SampleRate);
                _asioOut.AudioAvailable += OnAsioData;
            }
            else if (_isRecording)
            {
                _asioOut.InitRecordAndPlayback(null, 2, RecordingWaveFormat.SampleRate);
                _asioOut.AudioAvailable += OnAsioData;
            }
            else if (_isPlaying)
            {
                AudioDataProvider = new AudioDataProvider(_waveFormat, _buffer);
                _asioOut.Init(AudioDataProvider);
            }

            _isInitialized = true;
        }

        public override void Record()
        {
            if (!_isInitialized)
            {
                DoInit();
            }

            _cancellationTokenSource = new CancellationTokenSource();

            _asioOut?.Play();
        }

        public override void Play()
        {
            if (!_isInitialized)
            {
                DoInit();
            }
            _cancellationTokenSource = new CancellationTokenSource();
            AudioDataProvider.CancellationToken = _cancellationTokenSource.Token;

            _asioOut?.Play();
        }

        public override void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _asioOut?.Stop();
            _isInitialized = false;
        }

        public override void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _asioOut?.Stop();
            _asioOut?.Dispose();
            base.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}