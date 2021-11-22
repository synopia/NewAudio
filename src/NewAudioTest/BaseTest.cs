using System;
using System.Threading;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using NUnit.Framework;
using Serilog;
using VL.Lib.Basics.Resources;
using Xt;

namespace NewAudioTest
{
  

    public class BaseTest : IDisposable
    {
        protected BaseTest()
        {
        }

        protected void InitLogger<T>()
        {
        }

        public void Dispose()
        {
        }

        [SetUp]
        public void InitTest()
        {
        }

        [TearDown]
        public void EndTest()
        {
        }
    }
}