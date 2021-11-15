using System;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Core;

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
            Logger.Information("CREATE: AsioDevice ({DriverName})", driverName);
            _driverName = driverName;
            IsInputDevice = true;
            IsOutputDevice = true;
            _asioOut = new AsioOut(_driverName);
        }
/*
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
*/
        protected override bool Init()
        {
            if (_asioOut == null)
            {
                _asioOut = new AsioOut();
            }
            if (IsPlaying && IsRecording)
            {
                _asioOut.InitRecordAndPlayback(AudioDataProvider, RecordingParams.Channels.Value,
                    (int)RecordingParams.SamplingFrequency.Value);
                _asioOut.InputChannelOffset = RecordingParams.ChannelOffset.Value;
                _asioOut.ChannelOffset = PlayingParams.ChannelOffset.Value;
                _asioOut.AudioAvailable += OnAsioData;
                // RecordingParams.Channels = _asioOut.NumberOfInputChannels;
                // PlayingParams.Channels = _asioOut.NumberOfOutputChannels;
                PlayingParams.Latency.Value = _asioOut.PlaybackLatency;
                RecordingParams.Latency.Value = _asioOut.PlaybackLatency;
                // todo
                // PlayingParams.FrameSize = _asioOut.FramesPerBuffer;
                // RecordingParams.FrameSize = _asioOut.FramesPerBuffer;
                PlayingParams.Active.Value = true;
                RecordingParams.Active.Value = true;
            }
            else if (IsRecording)
            {
                _asioOut.InitRecordAndPlayback(null, RecordingParams.Channels.Value,
                    (int)RecordingParams.SamplingFrequency.Value);
                _asioOut.AudioAvailable += OnAsioData;
                // RecordingParams.Channels = _asioOut.NumberOfInputChannels;
                RecordingParams.Latency.Value = _asioOut.PlaybackLatency;
                // RecordingParams.FrameSize = _asioOut.FramesPerBuffer;
                PlayingParams.Active.Value = false;
                RecordingParams.Active.Value = true;
            }
            else if (IsPlaying)
            {
                _asioOut.Init(AudioDataProvider);
                // PlayingParams.Channels.Value = _asioOut.NumberOfOutputChannels;
                PlayingParams.Latency.Value = _asioOut.PlaybackLatency;
                // PlayingParams.FrameSize = _asioOut.FramesPerBuffer;
                PlayingParams.Active.Value = true;
                RecordingParams.Active.Value = false;
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