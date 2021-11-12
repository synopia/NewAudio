using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public void TestSingleton()
        {
            using var input = new InputDevice();
            using var output = new OutputDevice();
            input.Update(InputEnum, SamplingFrequency.Hz48000, 0, 1);
            output.Update(null, OutputEnum, SamplingFrequency.Hz48000, 0, 1);
            input.Lifecycle.WaitForEvents.WaitOne();
            output.Lifecycle.WaitForEvents.WaitOne();
            
            using var vd1 = DriverManager.Resource.GetInputDevice(InputEnum);
            Assert.AreSame(input.Device.Device, vd1.Device);
            using var vd2 = DriverManager.Resource.GetInputDevice(new InputDeviceSelection(InputEnum.Value));
            Assert.AreSame(input.Device.Device, vd2.Device);
            using var vd3 = DriverManager.Resource.GetOutputDevice(OutputEnum);
            Assert.AreSame(output.Device.Device, vd3.Device);
            using var vd4 = DriverManager.Resource.GetOutputDevice(new OutputDeviceSelection(OutputEnum.Value));
            Assert.AreSame(output.Device.Device, vd4.Device);

            using var vd5 = DriverManager.Resource.GetInputDevice(InputEnum);
            Assert.AreNotSame(input.Device, vd5);
            using var vd6 = DriverManager.Resource.GetOutputDevice(OutputEnum);
            Assert.AreNotSame(output.Device, vd6);
        }

        [Test]
        public void TestCreateInput2Ch()
        {
            using var input = new InputDevice();
            input.Update(InputEnum, SamplingFrequency.Hz48000, 0, 1);
            input.Lifecycle.WaitForEvents.WaitOne();
            
            var task = input.Device.CreateInput(new DeviceConfigRequest()
            {
                Channels = 2,
                AudioFormat = new AudioFormat(48000, 512, 2)
            });
            var res = task.GetAwaiter().GetResult();
            Assert.NotNull(res);
            Assert.NotNull(res.Item1);
            Assert.NotNull(res.Item2);
            
            Assert.AreEqual(2, res.Item1.Channels);
            Assert.AreEqual(0, res.Item1.ChannelOffset);
            Assert.AreEqual(2, res.Item1.AudioFormat.Channels);
            Assert.AreEqual(512, res.Item1.AudioFormat.SampleCount);
        }
        [Test]
        public void TestCreateInput2x2Ch()
        {
            using var input = new InputDevice();
            input.Update(InputEnum, SamplingFrequency.Hz48000, 0, 1);
            input.Lifecycle.WaitForEvents.WaitOne();
            var task1 = input.Device.CreateInput(new DeviceConfigRequest()
            {
                Channels = 2,
                AudioFormat = new AudioFormat(48000, 512, 2)
            });
            var task2 = input.Device.CreateInput(new DeviceConfigRequest()
            {
                Channels = 2,
                ChannelOffset = 2,
                AudioFormat = new AudioFormat(48000, 512, 2)
            });
            Task.WaitAll(new Task[] { task1, task2 });
            var res1 = task1.Result;
            var res2 = task2.Result;
            Assert.NotNull(res1);
            Assert.NotNull(res1.Item1);
            Assert.NotNull(res1.Item2);
            Assert.NotNull(res2);
            Assert.NotNull(res2.Item1);
            Assert.NotNull(res2.Item2);
            
            Assert.AreEqual(2, res1.Item1.Channels);
            Assert.AreEqual(0, res1.Item1.ChannelOffset);
            Assert.AreEqual(2, res1.Item1.AudioFormat.Channels);
            Assert.AreEqual(512, res1.Item1.AudioFormat.SampleCount);
            Assert.AreEqual(2, res2.Item1.Channels);
            Assert.AreEqual(2, res2.Item1.ChannelOffset);
            Assert.AreEqual(2, res2.Item1.AudioFormat.Channels);
            Assert.AreEqual(512, res2.Item1.AudioFormat.SampleCount);
            
            Assert.AreEqual(4, input.Device.Device.RecordingConfig.Channels);
            Assert.AreEqual(4, input.Device.Device.RecordingConfig.AudioFormat.Channels);
            Assert.AreEqual(48000, input.Device.Device.RecordingConfig.AudioFormat.SampleRate);
            Assert.AreEqual(512*4, input.Device.Device.RecordingConfig.AudioFormat.BufferSize);
        }
        
        [Test]
        public void TestLifecyclePlayStopPlay()
        {
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
            Assert.AreEqual(new[] { "Init" }, input.Device.MethodCalls(),
                string.Join(", ", input.Device.MethodCalls()));
            Assert.AreEqual(new[] { "Init" }, output.Device.MethodCalls(),
                string.Join(", ", output.Device.MethodCalls()));

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);
            Assert.AreEqual(new[] { "Init", "Start" }, input.Device.MethodCalls(),
                string.Join(", ",input.Device.MethodCalls()));
            Assert.AreEqual(new[] { "Init", "Start" }, output.Device.MethodCalls(),
                string.Join(", ", output.Device.MethodCalls()));

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);
            Assert.AreEqual(new[] { "Init", "Start" }, input.Device.MethodCalls(),
                string.Join(", ", input.Device.MethodCalls()));
            Assert.AreEqual(new[] { "Init", "Start" }, output.Device.MethodCalls(),
                string.Join(", ", output.Device.MethodCalls()));

            input.PlayParams.Playing.Value = false;
            output.PlayParams.Playing.Value = false;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Init, input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, output.Phase);
            Assert.AreEqual(new[] { "Init", "Start", "Stop" }, input.Device.MethodCalls(),
                string.Join(", ", input.Device.MethodCalls()));
            Assert.AreEqual(new[] { "Init", "Start", "Stop" }, output.Device.MethodCalls(),
                string.Join(", ", output.Device.MethodCalls()));

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            Wait(input, output);;
            Assert.AreEqual(LifecyclePhase.Play, input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);
            Assert.AreEqual(new[] { "Init", "Start", "Stop", "Start" }, input.Device.MethodCalls(),
                string.Join(", ", input.Device.MethodCalls()));
            Assert.AreEqual(new[] { "Init", "Start", "Stop", "Start" }, output.Device.MethodCalls(),
                string.Join(", ", output.Device.MethodCalls()));
        }


        [Test]
        public void TestLifecycleChangeDevice()
        {
            using var output = new OutputDevice();
            using var input = new InputDevice();

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
                    "Init", "Start", "Stop", "Stop","Dispose",
                    "Init", "Start", "Stop", "Stop","Dispose",
                    "Init", "Start"
                }, input.Device.MethodCalls(), string.Join(", ", input.Device.MethodCalls()));
            Assert.AreEqual(
                new[]
                {
                    "Init", "Start",  "Stop", "Stop","Dispose",
                    "Init", "Start", "Stop", "Stop","Dispose",
                    "Init", "Start"
                },
                output.Device.MethodCalls(), string.Join(", ", output.Device.MethodCalls()));
        }
    }
}