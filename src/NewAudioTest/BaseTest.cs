using System;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudioTest
{
    public class TestResourceHandle<T> : IResourceHandle<T>
    {
        public TestResourceHandle(T resource)
        {
            Resource = resource;
        }

        public void Dispose()
        {
            
        }

        public T Resource { get; }
    }
    public class VLTestApi : IVLApi
    {
        private DriverManager _driverManager = new DriverManager();
        private AudioService _audioService= new AudioService();
        private AudioGraph _audioGraph= new AudioGraph();

        public IResourceHandle<AudioService> GetAudioService()
        {
            return new TestResourceHandle<AudioService>(_audioService);
        }

        public IResourceHandle<AudioGraph> GetAudioGraph()
        {
            return new TestResourceHandle<AudioGraph>(_audioGraph);
        }

        public IResourceHandle<DriverManager> GetDriverManager()
        {
            return new TestResourceHandle<DriverManager>(_driverManager);
        }

        public IResourceHandle<IDevice> GetInputDevice(WaveInputDevice device)
        {
            return new TestResourceHandle<IDevice>((IDevice)device.Tag);
        }

        public IResourceHandle<IDevice> GetOutputDevice(WaveOutputDevice device)
        {
            return new TestResourceHandle<IDevice>((IDevice)device.Tag);
        }
    }
    public class BaseTest : IDisposable
    {
        protected ILogger Logger;
        private readonly IResourceHandle<AudioService> _audioService;

        protected BaseTest()
        {
            VLApi.Instance = new VLTestApi();
            _audioService = VLApi.Instance.GetAudioService();
        }
        
        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public void Dispose()
        {
            _audioService.Dispose();
        }

    }
}