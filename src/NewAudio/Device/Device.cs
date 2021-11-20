using System;
using System.Collections.Generic;
using NewAudio.Block;
using NewAudio.Core;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class DeviceParams : AudioParams
    {
        public AudioParam<int> SampleRate;
        public AudioParam<int> FramesPerBlock;
    }

    public interface IDevice: IDisposable
    {
        OutputDeviceBlock CreateOutput(IResourceHandle<IDevice> device, DeviceBlockFormat format);
        InputDeviceBlock CreateInput(IResourceHandle<IDevice> device, DeviceBlockFormat format);
        int MaxNumberOfInputChannels { get; }
        int MaxNumberOfOutputChannels { get; }
        int NumberOfInputChannels { get; }
        int NumberOfOutputChannels { get; }
        int SampleRate { get; }
        int FramesPerBlock { get; }
        string Name { get; }
        DeviceParams DeviceParams { get; }
        bool IsInitialized { get; }
        bool IsProcessing { get; }
        void Update();
        void Initialize();
        void Uninitialize();
        void EnableProcessing();
        void DisableProcessing();
        Action DeviceParamsWillChange { get; set; }
        Action DeviceParamsDidChange { get; set; }

    }

    public abstract class BaseDevice : IDevice
    {
        private readonly IResourceHandle<AudioService> _audioService;
        protected ILogger Logger;
        private bool _disposedValue;
        protected readonly DeviceManager DeviceManager;
        public Action DeviceParamsWillChange { get; set; }
        public Action DeviceParamsDidChange { get; set; }

        public int MaxNumberOfInputChannels { get; protected set; }
        public int MaxNumberOfOutputChannels{ get; protected  set; }
        public int NumberOfInputChannels { get; protected set; }
        public int NumberOfOutputChannels{ get; protected  set; }
        public int SampleRate => DeviceParams.SampleRate.Value;
        public int FramesPerBlock => DeviceParams.FramesPerBlock.Value;
        
        public abstract string Name { get; }

     
        public DeviceParams DeviceParams { get; }
        public abstract bool IsInitialized { get; protected set; }
        public abstract bool IsProcessing { get; protected set;}

        protected BaseDevice(DeviceManager deviceManager)
        {
            _audioService = Factory.GetAudioService();
            DeviceManager = deviceManager;
            DeviceParams = AudioParams.Create<DeviceParams>();
            DeviceParams.OnChange += UpdateFormat;
        }
        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public abstract OutputDeviceBlock CreateOutput(IResourceHandle<IDevice> device, DeviceBlockFormat format);
        public abstract InputDeviceBlock CreateInput(IResourceHandle<IDevice> device, DeviceBlockFormat format);

        protected abstract void UpdateFormat();

        public void Update()
        {
            if (DeviceParams.HasChanged)
            {
                DeviceParams.Update();
            }
        }
        public abstract void Initialize();
        public abstract void Uninitialize();
        public abstract void EnableProcessing();
        public abstract void DisableProcessing();

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for Driver {This} ({Disposing})", this.Name, disposing);
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