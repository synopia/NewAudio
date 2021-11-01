﻿using System;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Devices
{
    public class WaveDevice : BaseDevice
    {
        private ILogger _logger;
        private int _handle;
        private WaveOutEvent _waveOut;
        private WaveInEvent _waveIn;

        public WaveDevice(string name, bool isInputDevice, int handle)
        {
            _logger = AudioService.Instance.Logger.ForContext<WaveDevice>();
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = !isInputDevice;
            _handle = handle;
        }

        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
            _logger.Information("Init: Format={format}", waveFormat);
            AudioDataProvider = new AudioDataProvider(waveFormat, buffer);

                if (IsOutputDevice)
                {
                    _waveOut = new WaveOutEvent() { DeviceNumber = _handle, DesiredLatency = desiredLatency };
                    _waveOut?.Init(AudioDataProvider);

                }

        }

        private void DataAvailable(object sender, WaveInEventArgs evt)
        {
            
        }
        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
            if (IsInputDevice)
            {
                _waveIn = new WaveInEvent
                {
                    /*WaveFormat = format,*/ DeviceNumber = _handle, BufferMilliseconds = desiredLatency
                };
                _waveIn.DataAvailable += DataAvailable;
            }
        }

        public override void Record()
        {
            _waveIn?.StartRecording();
        }

        public override void Play()
        {
            _waveOut?.Play();
        }

        public override void Stop()
        {
            _waveIn?.StopRecording();
            _waveOut?.Stop();
        }

        public override void Dispose()
        {
            base.Dispose();
            _waveOut?.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}