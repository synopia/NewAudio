using System;
using System.Threading;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Devices
{
    public class DirectSoundDevice : BaseDevice
    {
        private DirectSoundOut _directSoundOut;

        private readonly Guid _guid;
        private readonly ILogger _logger;

        public DirectSoundDevice(string name, Guid guid)
        {
            _logger = AudioService.Instance.Logger.ForContext<DirectSoundDevice>();
            Name = name;
            _guid = guid;
            IsOutputDevice = true;
            IsInputDevice = false;
        }

        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
            AudioDataProvider = new AudioDataProvider(waveFormat, buffer);
            _directSoundOut = new DirectSoundOut(_guid, desiredLatency);
            _directSoundOut?.Init(AudioDataProvider);
            _logger.Information("DirectSound Out initialized.. ");
        }

        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
        }

        public override void Record()
        {
        }

        public override void Play()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            AudioDataProvider.CancellationToken = _cancellationTokenSource.Token;

            _directSoundOut?.Play();
        }

        public override void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _directSoundOut?.Stop();
        }

        public override void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _directSoundOut?.Stop();
            _directSoundOut?.Dispose();
            base.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}