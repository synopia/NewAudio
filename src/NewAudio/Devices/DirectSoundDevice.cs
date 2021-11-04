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
            CancellationTokenSource = new CancellationTokenSource();
            AudioDataProvider.CancellationToken = CancellationTokenSource.Token;

            _directSoundOut?.Play();
        }

        public override void Stop()
        {
            CancellationTokenSource?.Cancel();
            _directSoundOut?.Stop();
        }

        private bool _disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _directSoundOut?.Dispose();
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