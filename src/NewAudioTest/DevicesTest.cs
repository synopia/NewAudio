using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using NUnit.Framework;
using Serilog;
using SharedMemory;


namespace NewAudioTest
{
    using  NewAudioTest;
    
    [TestFixture]
    public class DevicesTest : BaseDeviceTest
    {
        
        [SetUp]
        public void Setup()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        }
        private void Wait(InputDevice input, OutputDevice output)
        {
            input.Lifecycle.WaitForEvents.WaitOne();
            output.Lifecycle.WaitForEvents.WaitOne();
        }

        [Test]
        public void TestLifecyclePlayStopPlay()
        {
            Init();
            using var input = new InputDevice();
            using var output = new OutputDevice();
            input.Update(null);
            output.Update(null, null);
            Wait(input, output);
            Assert.AreEqual(LifecyclePhase.Uninitialized, output.Phase);
            Assert.AreEqual(LifecyclePhase.Uninitialized, input.Phase);

            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);

            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Init, input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, output.Phase);
            Assert.AreEqual(new[] { "InitRecording" }, InputDevice.MethodCalls(),
                string.Join(", ", InputDevice.MethodCalls()));
            Assert.AreEqual(new[] { "InitPlayback" }, OutputDevice.MethodCalls(),
                string.Join(", ", OutputDevice.MethodCalls()));

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);
            Assert.AreEqual(new[] { "InitRecording", "Record" }, InputDevice.MethodCalls(),
                string.Join(", ",InputDevice.MethodCalls()));
            Assert.AreEqual(new[] { "InitPlayback", "Play" }, OutputDevice.MethodCalls(),
                string.Join(", ", OutputDevice.MethodCalls()));

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);
            Assert.AreEqual(new[] { "InitRecording", "Record" }, InputDevice.MethodCalls(),
                string.Join(", ", InputDevice.MethodCalls()));
            Assert.AreEqual(new[] { "InitPlayback", "Play" }, OutputDevice.MethodCalls(),
                string.Join(", ", OutputDevice.MethodCalls()));

            input.PlayParams.Playing.Value = false;
            output.PlayParams.Playing.Value = false;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Init, input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, output.Phase);
            Assert.AreEqual(new[] { "InitRecording", "Record", "Stop" }, InputDevice.MethodCalls(),
                string.Join(", ", InputDevice.MethodCalls()));
            Assert.AreEqual(new[] { "InitPlayback", "Play", "Stop" }, OutputDevice.MethodCalls(),
                string.Join(", ", OutputDevice.MethodCalls()));

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);
            Assert.AreEqual(new[] { "InitRecording", "Record", "Stop", "Record" }, InputDevice.MethodCalls(),
                string.Join(", ", InputDevice.MethodCalls()));
            Assert.AreEqual(new[] { "InitPlayback", "Play", "Stop", "Play" }, OutputDevice.MethodCalls(),
                string.Join(", ", OutputDevice.MethodCalls()));
        }


        [Test]
        public void TestLifecycleChangeDevice()
        {
            Init();
            using var output = new OutputDevice();
            using var input = new InputDevice();
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum, SamplingFrequency.Hz8000, 1);
            output.Update(input.Output, OutputEnum, SamplingFrequency.Hz8000, 1);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);

            input.PlayParams.Playing.Value = false;
            output.PlayParams.Playing.Value = false;
            input.Update(InputEnum, SamplingFrequency.Hz8000, 1);
            output.Update(input.Output, OutputEnum, SamplingFrequency.Hz8000, 1);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Init, input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, output.Phase);

            input.Update(InputNullEnum, SamplingFrequency.Hz8000, 2);
            output.Update(input.Output, OutputNullEnum, SamplingFrequency.Hz8000, 2);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Init, input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, output.Phase);

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum, SamplingFrequency.Hz8000, 1);
            output.Update(input.Output, OutputEnum, SamplingFrequency.Hz8000, 1);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);

            output.Update(input.Output, OutputNullEnum, SamplingFrequency.Hz8000, 2);
            Wait(input, output);;
            output.Update(input.Output, OutputEnum, SamplingFrequency.Hz8000, 1);
            Wait(input, output);;

            input.Update(InputNullEnum, SamplingFrequency.Hz8000, 2);
            Wait(input, output);;
            input.Update(InputEnum, SamplingFrequency.Hz8000, 1);

            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);

            Assert.AreEqual(
                new[]
                {
                    "InitRecording", "Record", "Stop", "Free",
                    "InitRecording", "Record", "Stop", "Free",
                    "InitRecording", "Record"
                }, InputDevice.MethodCalls(), string.Join(", ", InputDevice.MethodCalls()));
            Assert.AreEqual(
                new[]
                {
                    "InitPlayback", "Play", "Stop", "Free",
                    "InitPlayback", "Play", "Stop", "Free",
                    "InitPlayback", "Play"
                },
                OutputDevice.MethodCalls(), string.Join(", ", OutputDevice.MethodCalls()));
            // Assert.AreEqual(1, TestDeviceSetup.InputDevice.Threads.Distinct().Count());
            // Assert.AreEqual(mainThreadId, TestDeviceSetup.InputDevice.Threads[0]);
            // Assert.AreEqual(1, TestDeviceSetup.OutputDevice.Threads.Distinct().Count());
            // Assert.AreEqual(mainThreadId, TestDeviceSetup.OutputDevice.Threads[0]);
            // _input.Dispose();
            // _output.Dispose();
        }
    }
}