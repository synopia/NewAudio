using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;

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

        protected override Task<bool> Init()
        {
            _firstLoop = true;
            if (IsOutputDevice && IsPlaying)
            {
                var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                _wavePlayer = new WasapiOut(device, AudioClientShareMode.Shared, true, PlayingConfig.Latency);
                _wavePlayer.Init(AudioDataProvider);
                _wavePlayer.Play();
            } else if (IsInputDevice && IsRecording)
            {
                if (IsLoopback)
                {
                    var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                    _loopback = new WasapiLoopbackCapture(device);
                    _loopback.DataAvailable += DataAvailable;
                    RecordingConfig.AudioFormat = RecordingConfig.AudioFormat.WithWaveFormat(_loopback.WaveFormat);
                    _loopback.StartRecording();
                }
                else 
                {
                    var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                    _capture = new WasapiCapture(device)
                    {
                        WaveFormat = RecordingConfig.AudioFormat.WaveFormat
                    };
                    _capture.DataAvailable += DataAvailable;
                    RecordingConfig.AudioFormat = RecordingConfig.AudioFormat.WithWaveFormat(_capture.WaveFormat);
                    _capture.StartRecording();
                }
            }
            return Task.FromResult(true); 
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

        private void DataAvailable(object sender, WaveInEventArgs evt)
        {
            if (_firstLoop)
            {
                Logger.Information("Wasapi AudioIn Thread started (Writing to {recording} ({owner}))",
                    RecordingBuffer.Name, RecordingBuffer.IsOwnerOfSharedMemory);
                _firstLoop = false;
                _temp = new byte[RecordingBuffer.NodeBufferSize];
                _tempPos = 0;
            }

            // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            Logger.Verbose("DataAvailable {bytes}", evt.BytesRecorded / 4);

            try
            {
                var remaining = evt.BytesRecorded;
                var pos = 0;
                var token = CancellationTokenSource.Token;

                while (pos < evt.BytesRecorded && !token.IsCancellationRequested && !GenerateSilence)
                {
                    var toCopy = Math.Min(_temp.Length - _tempPos, remaining);
                    if (toCopy < 0 || toCopy>_temp.Length)
                    {
                        Logger.Error("HAE???? {l}-{p} = {t}", _temp.Length, _tempPos, toCopy);
                        break;
                    }
                    else
                    {
                        Array.Copy(evt.Buffer, pos, _temp, _tempPos, toCopy);
                    }

                    _tempPos += toCopy;
                    pos += toCopy;
                    remaining -= toCopy;

                    if (_tempPos == _temp.Length)
                    {
                        var written = RecordingBuffer.Write(_temp);
                        _tempPos = 0;
                        if (written != _temp.Length && !GenerateSilence)
                        {
                            Logger.Warning("Wrote to few bytes ({wrote}, expected: {expected})", written,
                                _temp.Length);
                        }
                    } 
                    
                }

                if (!GenerateSilence)
                {
                    if (pos != evt.BytesRecorded && !token.IsCancellationRequested)
                    {
                        Logger.Warning("pos!=buf {p}!={inc}", pos, evt.BytesRecorded);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("DataAvailable: {e}", e);
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