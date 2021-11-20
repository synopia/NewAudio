using System;
using System.Collections.Generic;
using NewAudio.Block;
using NewAudio.Core;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class DriverParams : AudioParams
    {
        public AudioParam<int> SampleRate;
        public AudioParam<int> FramesPerBlock;
    }

    public interface IDriver: IDisposable
    {
        public IResourceHandle<IDevice> CreateDevice(DeviceSelection selection, AudioBlockFormat format);
        int MaxNumberOfInputChannels { get; }
        int MaxNumberOfOutputChannels { get; }
        int SampleRate { get; }
        int FramesPerBlock { get; }
        string Name { get; }
        DriverParams DriverParams { get; }
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

    public abstract class BaseDriver : IDriver
    {
        private readonly IResourceHandle<AudioService> _audioService;
        protected ILogger Logger;
        private readonly List<IResourceHandle<IDevice>> _devices = new();
        protected readonly DriverManager DriverManager;
        public Action DeviceParamsWillChange { get; set; }
        public Action DeviceParamsDidChange { get; set; }

        public int MaxNumberOfInputChannels { get; protected set; }
        public int MaxNumberOfOutputChannels{ get; protected  set; }
        public int SampleRate => DriverParams.SampleRate.Value;
        public int FramesPerBlock => DriverParams.FramesPerBlock.Value;
        
        public abstract string Name { get; }

        protected abstract IDevice CreateDevice(string name,AudioBlockFormat format);

        public DriverParams DriverParams { get; }
        public abstract bool IsInitialized { get; protected set; }
        public abstract bool IsProcessing { get; protected set;}

        protected BaseDriver(DriverManager driverManager)
        {
            _audioService = Factory.GetAudioService();
            DriverManager = driverManager;
            DriverParams = AudioParams.Create<DriverParams>();
            DriverParams.OnChange += UpdateFormat;
        }

        protected abstract void UpdateFormat();

        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public void Update()
        {
            if (DriverParams.HasChanged)
            {
                DriverParams.Update();
            }
        }
        
        public IResourceHandle<IDevice> CreateDevice(DeviceSelection selection, AudioBlockFormat format)
        {
            var pool = ResourceProvider.New(() => CreateDevice(selection.Name, format), RemoveDevice);

            var handle = pool.GetHandle();
            _devices.Add(handle);
            return handle;
        }

        protected void RemoveDevice(IDevice d)
        {
            foreach (var handle in _devices.ToArray())
            {
                if (handle.Resource == d)
                {
                    _devices.Remove(handle);
                }
            }

            if (_devices.Count == 0)
            {
                DriverManager.RemoveDriver(this);
            }
        }
        
        private bool _disposedValue;

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
                    foreach (var handle in _devices)
                    {
                        handle?.Dispose();
                    }
                    _audioService?.Dispose();           
                }

                _disposedValue = true;
            }
        }

        public abstract void Initialize();
        public abstract void Uninitialize();
        public abstract void EnableProcessing();
        public abstract void DisableProcessing();
    }
}