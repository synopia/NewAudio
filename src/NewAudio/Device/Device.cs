using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Dsp;
using NewAudio.Internal;
using NewAudio.Nodes;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public interface IDevice : IDisposable
    {
        public OutputDeviceBlock Output { get; set; }
        int NumberOfInputChannels { get; }
        int NumberOfOutputChannels { get; }
        int InputChannelOffset { get; }
        int OutputChannelOffset { get; }
        int MaxNumberOfInputChannels { get; }
        int MaxNumberOfOutputChannels { get; }
        int SampleRate { get; }
        int FramesPerBlock { get; }
        string Name { get; }
        void Initialize();
        void Uninitialize();
        void EnableProcessing();
        void DisableProcessing();
        void Update();

        void UpdateFormat(int sampleRate, int framesPerBlock);
        Action DeviceParamsWillChange { get; set; }
        Action DeviceParamsDidChange { get; set; }
    }
    
    public abstract class BaseDevice : IDevice
    {
        private readonly IResourceHandle<AudioService> _audioService;
        protected readonly IDriver Driver;
        protected ILogger Logger;
        private int _sampleRate;
        private int _framesPerBlock;

        public abstract string Name { get; }
        public abstract int NumberOfInputChannels { get; }
        public abstract int NumberOfOutputChannels { get; }
        public abstract int InputChannelOffset { get; }
        public abstract int OutputChannelOffset { get; }
        public int MaxNumberOfInputChannels => Driver.MaxNumberOfInputChannels;
        public int MaxNumberOfOutputChannels => Driver.MaxNumberOfOutputChannels;

        public Action DeviceParamsWillChange
        {
            get=> Driver.DeviceParamsWillChange;
            set => Driver.DeviceParamsWillChange += value;
        }

        public Action DeviceParamsDidChange
        {
            get=> Driver.DeviceParamsDidChange;
            set => Driver.DeviceParamsDidChange += value;
        }

        public int SampleRate => Driver.SampleRate;

        public int FramesPerBlock => Driver.FramesPerBlock;

        public OutputDeviceBlock Output { get; set; }


        protected BaseDevice(IDriver driver)
        {
            _audioService = Factory.GetAudioService();
            Driver = driver;
        }

        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public void UpdateFormat(int sampleRate, int framesPerBlock)
        {
            Driver.DriverParams.SampleRate.Value = sampleRate;
            Driver.DriverParams.FramesPerBlock.Value = framesPerBlock;
        }

        public abstract void Initialize();
        public abstract void Uninitialize();
        public abstract void EnableProcessing();
        public abstract void DisableProcessing();

        public virtual void Update()
        {
            
        }

        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for Device {This} ({Disposing})", Name, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _audioService?.Dispose();
                }

                _disposedValue = true;
            }
        }
    }

}