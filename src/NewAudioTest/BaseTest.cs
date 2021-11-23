using System;
using System.Threading;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using NUnit.Framework;
using Serilog;
using VL.Lib.Basics.Resources;
using VL.NewAudio;
using Xt;

namespace NewAudioTest
{
  

    public class BaseTest : IDisposable
    {
        protected ILogger Logger;
        protected IXtPlatform Platform;
        protected  IAudioService AudioService;
        protected AudioGraph Graph;
        protected DeviceManager DeviceManager;
        protected bool MockAudio;
        protected BaseTest(bool mockAudio=true)
        {
            MockAudio = mockAudio;
        }

        public void Dispose()
        {
        }

        [SetUp]
        public void InitTest()
        {
            if (MockAudio)
            {
                Platform = new TestPlatform();
            }
            else
            {
                Platform = new RPlatform(XtAudio.Init("Test", IntPtr.Zero));
            }

            Resources.SetResources(Platform);

            Logger = Resources.GetLogger<BaseTest>();
            AudioService = Resources.GetAudioService();
            Graph = Resources.GetAudioGraph().Resource;
            DeviceManager = Resources.GetDeviceManager().Resource;

        }

        [TearDown]
        public void EndTest()
        {
            DeviceManager.Dispose();
            Graph.Dispose();
            AudioService.Dispose();
        }
    }
}