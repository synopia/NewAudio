using System;
using System.Threading;
using NewAudio.Processor;
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
        protected IAudioService AudioService;
        protected AudioGraph Graph;

        public void Dispose()
        {
        }
        
        [SetUp]
        public void Init()
        {
            Resources.SetResources(CreatePlatform);
            Logger = Resources.GetLogger<BaseTest>();
            AudioService = Resources.GetAudioService();
            Graph = Resources.GetAudioGraph().Resource;
        }

        protected virtual IXtPlatform CreatePlatform()
        {
            return new RPlatform(XtAudio.Init("Test", IntPtr.Zero));
        }

        [TearDown]
        public void EndTest()
        {
            Graph.Dispose();
            AudioService.Dispose();
        }
    }
}