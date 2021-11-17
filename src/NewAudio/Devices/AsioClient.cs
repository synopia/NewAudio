using System;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Nodes;

namespace NewAudio.Devices
{
    public class AsioClient : BaseAudioClient
    {
        private AsioOut _asioOut;
        private readonly string _driverName;
        private AudioDataProvider AudioDataProvider;
        public AsioClient(string name, string driverName)
        {
            Name = name;
            InitLogger<AsioClient>();
            Logger.Information("CREATE: AsioDevice ({DriverName})", driverName);
            _driverName = driverName;
            IsInputDevice = true;
            IsOutputDevice = true;
            _asioOut = new AsioOut(_driverName);
            OutputNode = new AsioOutput();
        }
    
        protected override bool Init()
        {
            if (_asioOut == null)
            {
                _asioOut = new AsioOut();
            }
            if (IsPlaying && IsRecording)
            {
                AudioDataProvider = new AudioDataProvider(Logger, PlayingParams.WaveFormat, OutputNode)
                {
                    CancellationToken = CancellationTokenSource.Token
                };
                _asioOut.InitRecordAndPlayback(AudioDataProvider, RecordingParams.Channels,
                    (int)RecordingParams.SamplingFrequency);
                _asioOut.InputChannelOffset = RecordingParams.ChannelOffset;
                _asioOut.ChannelOffset = PlayingParams.ChannelOffset;
                _asioOut.AudioAvailable += OnAsioData;
                // RecordingParams.Channels = _asioOut.NumberOfInputChannels;
                // PlayingParams.Channels = _asioOut.NumberOfOutputChannels;
                PlayingParams.Latency = _asioOut.PlaybackLatency;
                RecordingParams.Latency = _asioOut.PlaybackLatency;
                // todo
                // PlayingParams.FrameSize = _asioOut.FramesPerBuffer;
                // RecordingParams.FrameSize = _asioOut.FramesPerBuffer;
                PlayingParams.Active = true;
                RecordingParams.Active = true;
            }
            else if (IsRecording)
            {
                _asioOut.InitRecordAndPlayback(null, RecordingParams.Channels,
                    (int)RecordingParams.SamplingFrequency);
                _asioOut.AudioAvailable += OnAsioData;
                // RecordingParams.Channels = _asioOut.NumberOfInputChannels;
                RecordingParams.Latency = _asioOut.PlaybackLatency;
                // RecordingParams.FrameSize = _asioOut.FramesPerBuffer;
                RecordingParams.Active = true;
            }
            else if (IsPlaying)
            {
                AudioDataProvider = new AudioDataProvider(Logger, PlayingParams.WaveFormat, OutputNode)
                {
                    CancellationToken = CancellationTokenSource.Token
                };
                _asioOut.Init(AudioDataProvider);
                PlayingParams.Channels = _asioOut.NumberOfOutputChannels;
                PlayingParams.Latency = _asioOut.PlaybackLatency;
                PlayingParams.FramesPerBlock = _asioOut.FramesPerBuffer;
                PlayingParams.Active = true;
            }

            _asioOut.Play();
            return _asioOut.PlaybackState == PlaybackState.Playing;
        }

        protected override bool Stop()
        {
            if (_asioOut != null )
            {
                if (_asioOut.PlaybackState == PlaybackState.Playing)
                {
                    CancellationTokenSource?.Cancel();
                    _asioOut.Stop();
                }

                _asioOut.AudioAvailable -= OnAsioData;
                _asioOut.Dispose();
                _asioOut = null;
            }

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
                    if (_asioOut != null)
                    {
                        if (_asioOut.PlaybackState == PlaybackState.Playing)
                        {
                            CancellationTokenSource.Cancel();
                            _asioOut.Stop();
                        }

                        _asioOut.AudioAvailable -= OnAsioData;
                        _asioOut.Dispose();
                        _asioOut = null;
                    }
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