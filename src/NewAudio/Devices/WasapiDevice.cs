using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Devices
{
    public class WasapiDevice : BaseDevice
    {
        private readonly string _deviceId;
        private WasapiCapture _capture;
        private bool _firstLoop = true;

        private readonly ILogger _logger;
        private WasapiLoopbackCapture _loopback;
        private byte[] _temp;
        private int _tempPos;
        private WasapiOut _wavePlayer;

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

        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
            if (IsOutputDevice)
            {
                AudioDataProvider = new AudioDataProvider(waveFormat, buffer);
                var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                _wavePlayer = new WasapiOut(device, AudioClientShareMode.Shared, true, desiredLatency);
                _wavePlayer.Init(AudioDataProvider);
                _logger.Information("PLAYBACK Output Format: {format}",
                    _wavePlayer.OutputWaveFormat);
            }
        }

        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
            RecordingWaveFormat = waveFormat;
            RecordingBuffer = buffer;
            if (IsLoopback)
            {
                var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                _loopback = new WasapiLoopbackCapture(device);
                _loopback.DataAvailable += DataAvailable;
            }
            else if (IsInputDevice)
            {
                var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                _capture = new WasapiCapture(device)
                {
                    /*WaveFormat = format*/
                };
                _capture.DataAvailable += DataAvailable;
            }
        }


        private void DataAvailable(object sender, WaveInEventArgs evt)
        {
            if (_firstLoop)
            {
                _logger.Information("Wasapi AudioIn Thread started (Writing to {recording} ({owner}))",
                    RecordingBuffer.Name, RecordingBuffer.IsOwnerOfSharedMemory);
                _firstLoop = false;
                _temp = new byte[RecordingBuffer.NodeBufferSize];
                _tempPos = 0;
            }

            // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            _logger.Verbose("DataAvailable {bytes}", evt.BytesRecorded / 4);

            try
            {
                var remaining = evt.BytesRecorded;
                var pos = 0;
                var token = AudioService.Instance.Lifecycle.GetToken();

                while (pos < evt.BytesRecorded && !token.IsCancellationRequested)
                {
                    var toCopy = Math.Min(_temp.Length - _tempPos, remaining);
                    Array.Copy(evt.Buffer, pos, _temp, _tempPos, toCopy);
                    _tempPos += toCopy;
                    pos += toCopy;
                    remaining -= toCopy;

                    if (_tempPos == _temp.Length)
                    {
                        var written = RecordingBuffer.Write(_temp);
                        _tempPos = 0;
                        if (written != _temp.Length)
                            _logger.Warning("Wrote to few bytes ({wrote}, expected: {expected})", written,
                                _temp.Length);
                    }
                }

                if (pos != evt.BytesRecorded) _logger.Warning("pos!=buf {p}!={inc}", pos, evt.BytesRecorded);
            }
            catch (Exception e)
            {
                _logger.Error("DataAvailable: {e}", e);
            }
        }

        public override void Record()
        {
            _firstLoop = true;
            if (IsLoopback)
                _loopback?.StartRecording();
            else
                _capture?.StartRecording();
        }

        public override void Play()
        {
            _firstLoop = true;
            _wavePlayer?.Play();
        }

        public override void Stop()
        {
            _loopback?.StopRecording();
            _capture?.StopRecording();
            _wavePlayer?.Stop();
        }

        public override void Dispose()
        {
            _loopback?.Dispose();
            _capture?.Dispose();
            _wavePlayer?.Dispose();
            base.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}