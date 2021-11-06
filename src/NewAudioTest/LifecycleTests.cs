using System;
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

        private class TestDevice : ILifecycleDevice<int, bool>
        {
            public IList<string> Calls = new List<string>();

            public LifecyclePhase Phase { get; set; }
            public void ExceptionHappened(Exception e, string method)
            {
                Calls.Add($"Exception in {method}");
            }

            public Task<bool> CreateResources(int config)
            {
                Calls.Add("CreateResources");
                return Task.FromResult(true);
            }

            public Task<bool> FreeResources()
            {
                Calls.Add("FreeResources");
                return Task.FromResult(true);
            }

            public Task<bool> StartProcessing()
            {
                Calls.Add("StartProcessing");
                return Task.FromResult(true);
            }
            
            public Task<bool> StopProcessing()
            {
                Calls.Add("StopProcessing");
                return Task.FromResult(true);
            }
        }

        private ILogger log = AudioService.Instance.Logger;

        [Test]
        public void TestSimple()
        {
            var device = new TestDevice();
            var lf = new LifecycleStateMachine<int>();
            lf.EventHappens(LifecycleEvents.eCreate(1), device);
            Assert.AreEqual(LifecyclePhase.Ready, device.Phase);
            lf.EventHappens(LifecycleEvents.eStart, device);
            Assert.AreEqual(LifecyclePhase.Playing, device.Phase);
            lf.EventHappens(LifecycleEvents.eStop, device);
            Assert.AreEqual(LifecyclePhase.Ready, device.Phase);
            lf.EventHappens(LifecycleEvents.eStart, device);
            Assert.AreEqual(LifecyclePhase.Playing, device.Phase);
            lf.EventHappens(LifecycleEvents.eStop, device);
            Assert.AreEqual(LifecyclePhase.Ready, device.Phase);
            lf.EventHappens(LifecycleEvents.eFree, device);
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]{"CreateResources", 
                "StartProcessing", "StopProcessing", 
                "StartProcessing", "StopProcessing", 
                "FreeResources"}, device.Calls);
        }
        
        [Test]
        public void TestDoubles()
        {
            var device = new TestDevice();
            var lf = new LifecycleStateMachine<int>();
            lf.EventHappens(LifecycleEvents.eCreate(1), device);
            Assert.AreEqual(LifecyclePhase.Ready, device.Phase);
            lf.EventHappens(LifecycleEvents.eStart, device);
            Assert.AreEqual(LifecyclePhase.Playing, device.Phase);
            lf.EventHappens(LifecycleEvents.eStart, device);
            lf.EventHappens(LifecycleEvents.eStop, device);
            Assert.AreEqual(LifecyclePhase.Ready, device.Phase);
            lf.EventHappens(LifecycleEvents.eStop, device);
            lf.EventHappens(LifecycleEvents.eFree, device);
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]{"CreateResources", 
                "StartProcessing", "StopProcessing", 
                "FreeResources"}, device.Calls);
        }

        [Test]
        public void TestReplay()
        {
            var device = new TestDevice();
            var lf = new LifecycleStateMachine<int>();
            lf.EventHappens(LifecycleEvents.eCreate(1), device);
            Assert.AreEqual(LifecyclePhase.Ready, device.Phase);
            lf.EventHappens(LifecycleEvents.eStart, device);
            Assert.AreEqual(LifecyclePhase.Playing, device.Phase);
            lf.EventHappens(LifecycleEvents.eCreate(1), device);
            Assert.AreEqual(LifecyclePhase.Playing, device.Phase);
            lf.EventHappens(LifecycleEvents.eFree, device);
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]{"CreateResources", 
                "StartProcessing", "StopProcessing",
                "FreeResources", "CreateResources",
                "StartProcessing", "StopProcessing",
                "FreeResources"}, device.Calls, string.Join(", ",device.Calls));
        }
        
    }
}