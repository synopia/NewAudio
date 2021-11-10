﻿using System;
using System.Threading;
using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class AudioBufferOutTest
    {
        [SetUp]
        public void Setup()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        }
        [Test]
        public void TestIt()
        {
            AudioService.Instance.Init();
            TestDeviceSetup.Init();
            
            var buf = new AudioBufferOut();
            using var input = new InputDevice();
            
            input.Update(TestDeviceSetup.InputEnum, SamplingFrequency.Hz48000, 0, 1);
            input.Lifecycle.WaitForEvents.WaitOne();
        
            buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);
            buf.Lifecycle.WaitForEvents.WaitOne();
            
            input.PlayParams.Playing.Value = true;
            buf.PlayParams.Playing.Value = true;
            input.Update(TestDeviceSetup.InputEnum, SamplingFrequency.Hz48000, 0, 1);
            input.Lifecycle.WaitForEvents.WaitOne();
            buf.Lifecycle.WaitForEvents.WaitOne();

            var spread = buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);
            buf.Lifecycle.WaitForEvents.WaitOne();
            TestDeviceSetup.InputDevice.RecordBuffer.Write(new byte[512 * 2 * 4]);
            TestDeviceSetup.InputDevice.RecordBuffer.Write(new byte[512 * 2 * 4]);
            Task.Delay(100).Wait();
            spread = buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);
            
            Assert.IsEmpty(buf.ErrorMessages());
            Assert.AreEqual(LifecyclePhase.Play, buf.Phase);
            Assert.AreEqual(1024, spread.Count);
            
        }
    }
}