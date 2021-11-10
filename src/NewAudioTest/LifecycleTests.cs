﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Abodit.StateMachine;
using NewAudio.Core;
using NUnit.Framework;
using Serilog;

namespace NewAudioTest
{
    [TestFixture]
    public class LifecycleTests
    {
        [SetUp]
        public void Setup()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        }
        private class TestDevice : ILifecycleDevice
        {
            public IList<string> Calls = new List<string>();

            public LifecyclePhase Phase { get; set; }
            public void ExceptionHappened(Exception e, string method)
            {
                Calls.Add($"Exception in {method}");
            }

            public bool IsInitValid()
            {
                return true;
            }
            public bool IsPlayValid()
            {
                return true;
            }

            public async Task<bool> Init()
            {
                Calls.Add("Create");
                await Task.Delay(10);
                return true;
            }

            public bool Play()
            {
                Calls.Add("Play");
                return true;
            }

            public Task<bool> Free()
            {
                Calls.Add("Free");
                return Task.FromResult(true);
            }

            public bool Stop()
            {
                Calls.Add("Stop");
                return true;
            }
        }

        private ILogger log = AudioService.Instance.Logger;

        [Test]
        public void TestSimple()
        {
            var device = new TestDevice();
            var lf = new LifecycleStateMachine(device);
            lf.EventHappens(LifecycleEvents.eInit);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.ePlay);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.eStop);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.ePlay);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.eStop);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.eFree);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]{"Create", 
                "Play", "Stop", 
                "Play", "Stop", 
                "Free"}, device.Calls);
        }
        
      
        [Test]
        public void TestFast()
        {
            var device = new TestDevice();
            var lf = new LifecycleStateMachine(device);
            lf.EventHappens(LifecycleEvents.eInit);
            lf.EventHappens(LifecycleEvents.ePlay);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.ePlay);
            lf.EventHappens(LifecycleEvents.eStop);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.eStop);
            lf.EventHappens(LifecycleEvents.eFree);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]{"Create", 
                "Play", "Play", "Stop", 
                "Free"}, device.Calls);
        }

        [Test]
        public void TestReplay()
        {
            var device = new TestDevice();
            var lf = new LifecycleStateMachine(device);
            lf.EventHappens(LifecycleEvents.eInit);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.ePlay);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.eInit);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.eFree);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]{"Create", 
                "Play", "Stop",
                "Free", "Create",
                "Play", "Stop",
                "Free"}, device.Calls, string.Join(", ",device.Calls));
        }
        
    }
}