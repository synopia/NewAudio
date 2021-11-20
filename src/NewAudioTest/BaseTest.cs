using System;
using System.Threading;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using NUnit.Framework;
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

    public class VLTestApi 
    {
        private DeviceManager _deviceManager = new();
        private AudioService _audioService = new();
        private AudioGraph _audioGraph = new();

        public IResourceHandle<AudioService> GetAudioService()
        {
            return new TestResourceHandle<AudioService>(_audioService);
        }

        public IResourceHandle<AudioGraph> GetAudioGraph()
        {
            return new TestResourceHandle<AudioGraph>(_audioGraph);
        }

        public IResourceHandle<DeviceManager> GetDriverManager()
        {
            return new TestResourceHandle<DeviceManager>(_deviceManager);
        }
    }

    public class BaseTest : IDisposable
    {
        protected ILogger Logger;
        private IResourceHandle<AudioService> _audioService;

        protected BaseTest()
        {
            _audioService = Factory.GetAudioService();
        }

        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public void Dispose()
        {
            _audioService.Dispose();
        }

        [SetUp]
        public void InitTest()
        {
        }
    }
}