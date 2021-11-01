using System;
using System.Threading;
using NAudio.Wave;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Devices
{
    public class DirectSoundDevice: BaseDevice
    {

        private Guid _guid;
        private DirectSoundOut _directSoundOut;
        public DirectSoundDevice(string name, Guid guid)
        {
            Name = name;
            _guid = guid;
            IsOutputDevice = true;
            IsInputDevice = false;
        }

        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop)
        {
            PlayPauseStop = playPauseStop;
            AudioDataProvider = new AudioDataProvider(waveFormat, buffer, PlayPauseStop);
            _directSoundOut = new DirectSoundOut(_guid, desiredLatency);
            _directSoundOut?.Init(AudioDataProvider);
            Logger.Information("DirectSound Out initialized.. ");
        }

        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop)
        {
        }

        public override void Record()
        {
            
        }

        public override void Play()
        {
            Logger.Information("DirectSound Out Play");
            _directSoundOut?.Play();
        }

        public override void Stop()
        {
            Logger.Information("DirectSound Out Stop");
            _directSoundOut?.Stop();
        }

        public override void Dispose()
        {
            Logger.Information("DirectSound Out Dispose");
            base.Dispose();
            _directSoundOut?.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }

    }
}