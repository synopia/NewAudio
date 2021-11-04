﻿using System;
using System.Threading;
using NAudio.Wave;
using SharedMemory;
using NewAudio.Core;

namespace NewAudio.Devices
{
    public interface IDevice : IDisposable
    {
        public string Name { get; }
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }
        AudioDataProvider AudioDataProvider { get; }
        public void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat);
        public void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat);
        public void Play();

        public void Record();
        public void Stop();
    }

    public abstract class BaseDevice : IDevice
    {
        public WaveFormat RecordingWaveFormat { get; protected set; }
        public CircularBuffer RecordingBuffer { get; protected set; }

        public AudioDataProvider AudioDataProvider { get; protected set; }

        public string Name { get; protected set; }

        public bool IsInputDevice { get; protected set; }

        public bool IsOutputDevice { get; protected set; }
        protected CancellationTokenSource CancellationTokenSource;
        
        public abstract void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat);

        public abstract void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat);

        public abstract void Play();

        public abstract void Record();

        public abstract void Stop();

        private bool _disposedValue;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    
                }

                _disposedValue = true;
            }
        }
    }
}