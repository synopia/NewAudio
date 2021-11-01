using System;
using System.Threading;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Devices
{
    public interface IDevice: IDisposable
    {
        public string Name { get; }
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }
        public void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop);
        public void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop);
        public void Play();
        
        public void Record();
        public void Stop();
    }

    public abstract class BaseDevice : IDevice
    {
        protected ILogger Logger;

        protected PlayPauseStop PlayPauseStop { get; set; }

        public WaveFormat RecordingWaveFormat { get; protected set; }
        public CircularBuffer RecordingBuffer { get; protected set; }

        public AudioDataProvider AudioDataProvider { get; protected set; }

        public string Name { get; protected set; }

        public bool IsInputDevice { get;protected set; }

        public bool IsOutputDevice { get;protected set; }
        
        protected BaseDevice()
        {
            Logger = AudioService.Instance.Logger;
        }
        public abstract void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop);

        public abstract void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat,
            PlayPauseStop playPauseStop);

        public abstract void Play();

        public abstract void Record();

        public abstract void Stop();

        public virtual void Dispose()
        {
            Logger.Information("Cancel AUDIO THREAD");
            
            AudioDataProvider?.Dispose();
        }
    }

}