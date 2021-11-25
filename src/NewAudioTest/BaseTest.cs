using System;
using System.Threading;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using NUnit.Framework;
using Serilog;
using NewAudio;
using Xt;

namespace NewAudioTest
{
  

    public class BaseTest : IDisposable
    {
        protected ILogger Logger;
        protected Func<IXtPlatform> PlatformFunc;
        protected IXtPlatform Platform;
        protected  IAudioService AudioService;
        protected AudioGraph Graph;
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
                PlatformFunc = ()=>new TestPlatform();
            }
            else
            {
                PlatformFunc = ()=>new RPlatform(XtAudio.Init("Test", IntPtr.Zero));
            }

            Resources.SetResources(PlatformFunc);
            Logger = Resources.GetLogger<BaseTest>();
            AudioService = Resources.GetAudioService();
            Graph = Resources.GetAudioGraph().Resource;
        }

        [TearDown]
        public void EndTest()
        {
            Graph.Dispose();
            AudioService.Dispose();
        }
    }
}