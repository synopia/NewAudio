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

        private readonly ILogger _logger;
        private WasapiLoopbackCapture _loopback;
        private WasapiOut _wavePlayer;
        private DeviceConfig _recording;

        private byte[] _temp;
        private int _tempPos;

        public WasapiDevice(string name, bool isInputDevice, bool isLoopback, string deviceId)
        {
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = !isInputDevice;
            IsLoopback = isLoopback;
            _deviceId = deviceId;
            _logger = AudioService.Instance.Logger.ForContext<WasapiDevice>();
        }

        private bool IsLoopback { get; }

        public override Task<DeviceConfigResponse> Create(DeviceConfigRequest config)
        {
            CancellationTokenSource = new CancellationTokenSource();
            var configResponse = new DeviceConfigResponse()
            {
                SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
            };

            if (IsOutputDevice && config.IsPlaying)
            {
                AudioDataProvider = new AudioDataProvider(config.Playing.WaveFormat, config.Playing.Buffer)
                {
                    CancellationToken = CancellationTokenSource.Token
                };
                var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                _wavePlayer = new WasapiOut(device, AudioClientShareMode.Shared, true, config.Playing.Latency);
                _wavePlayer.Init(AudioDataProvider);
                configResponse.PlayingWaveFormat = _wavePlayer.OutputWaveFormat;
                configResponse.PlayingChannels = 2;
                configResponse.DriverPlayingChannels = 2;
            }
            else if (IsInputDevice && config.IsRecording)
            {
                configResponse.RecordingChannels = 2;
                configResponse.DriverRecordingChannels = 2;

                if (IsLoopback)
                {
                    var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                    _loopback = new WasapiLoopbackCapture(device);
                    _loopback.DataAvailable += DataAvailable;
                    config.Recording.WaveFormat = _loopback.WaveFormat;
                }
                else if (IsInputDevice)
                {
                    var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                    _capture = new WasapiCapture(device)
                    {
                        WaveFormat = config.Recording.WaveFormat
                    };
                    _capture.DataAvailable += DataAvailable;
                    config.Recording.WaveFormat = _capture.WaveFormat;
                }

                _recording = new DeviceConfig()
                {
                    Buffer = config.Recording.Buffer
                };
            }

            return Task.FromResult(configResponse);
        }

        public override Task<bool> Free()
        {
            CancellationTokenSource?.Cancel();

            _loopback?.StopRecording();
            _capture?.StopRecording();
            _wavePlayer?.Stop();
            _loopback?.Dispose();
            _capture?.Dispose();
            _wavePlayer?.Dispose();
            return Task.FromResult(true);
        }

        public override bool Start()
        {
            CancellationTokenSource = new CancellationTokenSource();
            if (AudioDataProvider != null)
            {
                AudioDataProvider.CancellationToken = CancellationTokenSource.Token;
            }

            _firstLoop = true;
            _wavePlayer?.Play();
            _loopback?.StartRecording();
            _capture?.StartRecording();

            return true;
        }

        public override bool Stop()
        {
            _loopback?.StopRecording();
            _capture?.StopRecording();
            _wavePlayer?.Stop();
            CancellationTokenSource?.Cancel();
            return true;
        }

        private void DataAvailable(object sender, WaveInEventArgs evt)
        {
            if (_firstLoop)
            {
                _logger.Information("Wasapi AudioIn Thread started (Writing to {recording} ({owner}))",
                    _recording.Buffer.Name, _recording.Buffer.IsOwnerOfSharedMemory);
                _firstLoop = false;
                _temp = new byte[_recording.Buffer.NodeBufferSize];
                _tempPos = 0;
            }

            // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            _logger.Verbose("DataAvailable {bytes}", evt.BytesRecorded / 4);

            try
            {
                var remaining = evt.BytesRecorded;
                var pos = 0;
                var token = CancellationTokenSource.Token;

                while (pos < evt.BytesRecorded && !token.IsCancellationRequested)
                {
                    var toCopy = Math.Min(_temp.Length - _tempPos, remaining);
                    Array.Copy(evt.Buffer, pos, _temp, _tempPos, toCopy);
                    _tempPos += toCopy;
                    pos += toCopy;
                    remaining -= toCopy;

                    if (_tempPos == _temp.Length)
                    {
                        var written = _recording.Buffer.Write(_temp);
                        _tempPos = 0;
                        if (written != _temp.Length)
                        {
                            _logger.Warning("Wrote to few bytes ({wrote}, expected: {expected})", written,
                                _temp.Length);
                        }
                    }
                }

                if (pos != evt.BytesRecorded && !token.IsCancellationRequested)
                {
                    _logger.Warning("pos!=buf {p}!={inc}", pos, evt.BytesRecorded);
                }
            }
            catch (Exception e)
            {
                _logger.Error("DataAvailable: {e}", e);
            }
        }


        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _loopback?.Dispose();
                    _capture?.Dispose();
                    _wavePlayer?.Dispose();
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