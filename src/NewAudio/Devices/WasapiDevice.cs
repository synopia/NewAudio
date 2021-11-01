using System;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Devices
{
    public class WasapiDevice : BaseDevice
    {
        public bool IsLoopback { get; }

        private WasapiOut _wavePlayer;
        private WasapiLoopbackCapture _loopback;
        private WasapiCapture _capture;
        private string _deviceId;

        public WasapiDevice(string name, bool isInputDevice, bool isLoopback, string deviceId)
        {
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = !isInputDevice;
            IsLoopback = isLoopback;
            _deviceId = deviceId;
        }

        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop)
        {
            if (IsOutputDevice)
            {
                PlayPauseStop = playPauseStop;
                AudioDataProvider = new AudioDataProvider(waveFormat, buffer, PlayPauseStop);
                var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                _wavePlayer = new WasapiOut(device, AudioClientShareMode.Shared, true, desiredLatency);
                _wavePlayer.Init(AudioDataProvider);
                AudioService.Instance.Logger.Information("PLAYBACK Output Format: {format}",
                    _wavePlayer.OutputWaveFormat);
            }
        }

        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop)
        {
            RecordingWaveFormat = waveFormat;
            RecordingBuffer = buffer;
            PlayPauseStop = playPauseStop;
            if (IsLoopback)
            {
                var device = new MMDeviceEnumerator().GetDevice(_deviceId);
                _loopback = new WasapiLoopbackCapture(device);

                _loopback.DataAvailable += DataAvailable;
                var format = _loopback.WaveFormat;
                Logger.Information("Starting Wasapi Recording, T={T}, CH={CH}, ENC={ENC}",
                    RecordingWaveFormat.SampleRate, RecordingWaveFormat.Channels, RecordingWaveFormat.Encoding);
                Logger.Information("Format2 T={T}, CH={CH}, ENC={ENC}", format.SampleRate, format.Channels,
                    format.Encoding);
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
            AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            Logger.Verbose("DataAvailable {bytes}", evt.BytesRecorded);
            try
            {
                int remaining = evt.BytesRecorded;
                int pos = 0;
                var cancellationToken = PlayPauseStop.GetToken();
                while (remaining > 0 && !cancellationToken.IsCancellationRequested)
                {
                    if (remaining < 2048)
                    {
                        Buffers.WriteAll(RecordingBuffer, evt.Buffer, pos, remaining, cancellationToken);
                        remaining = 0;
                    }
                    else
                    {
                        var written = RecordingBuffer.Write(evt.Buffer, pos);
                        pos += written;
                        remaining -= written;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("{e}", e);
            }
        }

        public override void Record()
        {
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
            _wavePlayer?.Play();
        }

        public override void Stop()
        {
            _loopback?.StopRecording();
            _capture?.StopRecording();
            _wavePlayer?.Stop();
            _loopback?.Dispose();
            _capture?.Dispose();
            _wavePlayer?.Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
            _loopback?.Dispose();
            _capture?.Dispose();
            _wavePlayer?.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}