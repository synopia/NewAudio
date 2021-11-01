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
        private bool IsLoopback { get; }

        private ILogger _logger;
        private WasapiOut _wavePlayer;
        private WasapiLoopbackCapture _loopback;
        private WasapiCapture _capture;
        private readonly string _deviceId;
        private bool _firstLoop = true;
        
        public WasapiDevice(string name, bool isInputDevice, bool isLoopback, string deviceId)
        {
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = !isInputDevice;
            IsLoopback = isLoopback;
            _deviceId = deviceId;
            _logger = AudioService.Instance.Logger.ForContext<WasapiDevice>();
        }

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
                _logger.Information("Wasapi AudioIn Thread started");
                _firstLoop = false;
            }
            // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            _logger.Verbose("DataAvailable {bytes}", evt.BytesRecorded);
            try
            {
                int remaining = evt.BytesRecorded;
                int pos = 0;
                var token = AudioService.Instance.Lifecycle.GetToken();
                while (pos < evt.BytesRecorded && !token.IsCancellationRequested)
                {
                    var written = Buffers.Write(RecordingBuffer, evt.Buffer, pos, remaining);
                    pos += written;
                    remaining -= written;
                }
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
            {
                _loopback?.StartRecording();
            }
            else
            {
                _capture?.StartRecording();
            }
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