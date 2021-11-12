using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewAudio.Core;
using NUnit.Framework;
using Serilog;

namespace NewAudioTest
{
    [TestFixture]
    public class LifecycleTests : BaseTest
    {
        public LifecycleTests()
        {
            InitLogger<LifecycleTests>();
        }

        [SetUp]
        public void Setup()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        }


        private class TestDevice : ILifecycleDevice
        {
            public IList<string> Calls = new List<string>();
            private ILogger _logger;
            public LifecyclePhase Phase { get; set; }

            public TestDevice(ILogger logger)
            {
                _logger = logger;
            }

            public void ExceptionHappened(Exception e, string method)
            {
                Calls.Add($"Exception in {method}");
            }

            public ILogger Logger => _logger;

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

        [Test]
        public void TestSimple()
        {
            var device = new TestDevice(Logger);
            var lf = new LifecycleStateMachine(device);
            lf.EventHappens(LifecycleEvents.EInit);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.EPlay);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.EStop);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.EPlay);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.EStop);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.EFree);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]
            {
                "Create",
                "Play", "Stop",
                "Play", "Stop",
                "Free"
            }, device.Calls);
        }


        [Test]
        public void TestFast()
        {
            var device = new TestDevice(Logger);
            var lf = new LifecycleStateMachine(device);
            lf.EventHappens(LifecycleEvents.EInit);
            lf.EventHappens(LifecycleEvents.EPlay);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.EPlay);
            lf.EventHappens(LifecycleEvents.EStop);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.EStop);
            lf.EventHappens(LifecycleEvents.EFree);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]
            {
                "Create",
                "Play", "Play", "Stop",
                "Free"
            }, device.Calls);
        }

        [Test]
        public void TestReplay()
        {
            var device = new TestDevice(Logger);
            var lf = new LifecycleStateMachine(device);
            lf.EventHappens(LifecycleEvents.EInit);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Init, device.Phase);
            lf.EventHappens(LifecycleEvents.EPlay);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.EInit);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Play, device.Phase);
            lf.EventHappens(LifecycleEvents.EFree);
            lf.WaitForEvents.WaitOne();
            Assert.AreEqual(LifecyclePhase.Uninitialized, device.Phase);

            Assert.AreEqual(new string[]
            {
                "Create",
                "Play", "Stop",
                "Free", "Create",
                "Play", "Stop",
                "Free"
            }, device.Calls, string.Join(", ", device.Calls));
        }
    }
}