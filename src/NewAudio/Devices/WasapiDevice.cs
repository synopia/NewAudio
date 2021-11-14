using System;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NewAudio.Devices
{
    public class WasapiDevice : BaseDevice
    {
        private readonly string _deviceId;
        private WasapiCapture _capture;
        private bool _firstLoop = true;

        private WasapiLoopbackCapture _loopback;
        private WasapiOut _wavePlayer;

        private byte[] _temp;
        private int _tempPos;

        public WasapiDevice(string name, bool isInputDevice, bool isLoopback, string deviceId)
        {
            InitLogger<WasapiDevice>();
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = !isInputDevice;
            IsLoopback = isLoopback;
            _deviceId = deviceId;
        }

        private bool IsLoopback { get; }

        protected override bool Init()
        {
            _firstLoop = true;
            if (IsOutputDevice && IsPlaying)
            {
                var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                _wavePlayer = new WasapiOut(device, AudioClientShareMode.Shared, true, PlayingParams.Latency.Value);
                _wavePlayer.Init(AudioDataProvider);
                _wavePlayer.Play();
                RecordingParams.Active.Value = false;
                PlayingParams.Active.Value = true;
            } else if (IsInputDevice && IsRecording)
            {
                if (IsLoopback)
                {
                    var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                    _loopback = new WasapiLoopbackCapture(device);
                    _loopback.DataAvailable += DataAvailable;
                    // todo
                    // _recordingConfig.AudioFormat = _recordingConfig.AudioFormat.WithWaveFormat(_loopback.WaveFormat);
                    _loopback.StartRecording();
                    RecordingParams.WaveFormat.Value = _loopback.WaveFormat;
                }
                else 
                {
                    var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                    _capture = new WasapiCapture(device)
                    {
                        WaveFormat = RecordingParams.AudioFormat.WaveFormat
                    };
                    _capture.DataAvailable += DataAvailable;
                    _capture.StartRecording();
                    RecordingParams.WaveFormat.Value = _capture.WaveFormat;
                }
                RecordingParams.Active.Value = true;
                PlayingParams.Active.Value = false;
            }

            return true;
        }

        public override string DebugInfo()
        {
            var info = IsPlaying ? $"{_wavePlayer?.PlaybackState}" :
                IsLoopback ? $"{_loopback?.CaptureState}" : $"{_capture?.CaptureState}";
            return $"[{this}, {info}, {base.DebugInfo()}]";
        }

        private void DataAvailable(object sender, WaveInEventArgs evt)
        {
            if (_firstLoop)
            {
                Logger.Information("Wasapi AudioIn Thread started");
                _firstLoop = false;
                _temp = new byte[RecordingParams.AudioFormat.BufferSize*RecordingParams.AudioFormat.BytesPerSample];
                _tempPos = 0;
            }

            // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            Logger.Verbose("DataAvailable {Bytes}", evt.BytesRecorded / 4);

            try
            {
                var remaining = evt.BytesRecorded;
                var pos = 0;
                var token = CancellationTokenSource.Token;

                while (pos < evt.BytesRecorded && !token.IsCancellationRequested && !GenerateSilence)
                {
                    var toCopy = Math.Min(_temp.Length - _tempPos, remaining);
                    if (toCopy < 0 || toCopy > _temp.Length)
                    {
                        throw new Exception("Wrong writing position! Multiple threads are operating?");
                    }

                    Array.Copy(evt.Buffer, pos, _temp, _tempPos, toCopy);

                    _tempPos += toCopy;
                    pos += toCopy;
                    remaining -= toCopy;

                    if (_tempPos == _temp.Length)
                    {
                        
                        OnDataReceived(_temp);
                        // var written = RecordingBuffer.Write(_temp);
                        _tempPos = 0;
                        // if (written != _temp.Length && !GenerateSilence)
                        // {
                            // Logger.Warning("Wrote to few bytes ({Wrote}, expected: {Expected})", written,
                                // _temp.Length);
                        // }
                    }
                }

                /*
                if (!GenerateSilence)
                {
                    if (pos != evt.BytesRecorded && !token.IsCancellationRequested)
                    {
                        Logger.Warning("pos!=buf {Pos}!={Read}", pos, evt.BytesRecorded);
                    }
                }
            */
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception happened in Wasapi Reader");
                throw;
            }
        }


        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    CancellationTokenSource?.Cancel();
                    if (_loopback != null)
                    {
                        _loopback.StopRecording();
                        _loopback.DataAvailable -= DataAvailable;
                        _loopback.Dispose();
                    }

                    if (_capture != null)
                    {
                        _capture.StopRecording();
                        _capture.DataAvailable -= DataAvailable;
                        _capture.Dispose();
                    }

                    if (_wavePlayer != null)
                    {
                        _wavePlayer.Stop();
                        _wavePlayer.Dispose();
                    }


                    _loopback = null;
                    _capture = null;
                    _wavePlayer = null;
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