using System;
using System.Threading;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Devices
{
    public class DirectSoundDevice: BaseDevice
    {
        private ILogger _logger;

        private Guid _guid;
        private DirectSoundOut _directSoundOut;
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
            _directSoundOut?.Play();
        }

        public override void Stop()
        {
            _directSoundOut?.Stop();
        }

        public override void Dispose()
        {
            _directSoundOut?.Dispose();
            base.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }

    }
}