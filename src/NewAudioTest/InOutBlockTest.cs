using System;
using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Internal;
using NewAudio.Nodes;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class InOutBlockTest : BaseDeviceTest
    {
        public void WaitAndAssert(LifecyclePhase phase, params IAudioNode[] args)
        {
            Wait(args);
            foreach (var node in args)
            {
                Assert.IsEmpty(node.ErrorMessages());
                Assert.AreEqual(phase, node.Phase);
            }
        }

        [Test]
        public void TestRead2ChI_Write2ChI()
        {
            using var input = new InputDevice();
            using var output = new OutputDevice();

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputDevice);
            output.Update(input.Output, OutputDevice);
            WaitAndAssert(LifecyclePhase.Play, input, output);
            input.Update(InputDevice);
            output.Update(input.Output, OutputDevice);
            input.Update(InputDevice);
            output.Update(input.Output, OutputDevice);


            Assert.AreEqual(1024, input.AudioFormat.BufferSize);
            var inputSignal = BuildSignal(input.Output.Format);
            input.Device.OnDataReceived(MixBuffers.CopyFloatToByte(inputSignal));
            Task.Delay(100).GetAwaiter().GetResult();
            DriverManager.Resource.UpdateAllDevices();
            output.ActualDeviceParams.Update().Wait();

            Assert.AreEqual(1024, output.AudioFormat.BufferSize);
            var outputSignal = new float[output.AudioFormat.BufferSize];
            // output.Device.PlayingBuffer().Read(outputSignal);

            AssertSignal(inputSignal, outputSignal);
        }

        [Test]
        public void TestRead2x1ChI_Write2x1Ch()
        {
            using var input1 = new InputDevice();
            using var input2 = new InputDevice();
            using var output1 = new OutputDevice();
            using var output2 = new OutputDevice();

            input1.PlayParams.Playing.Value = true;
            input2.PlayParams.Playing.Value = true;
            output1.PlayParams.Playing.Value = true;
            output2.PlayParams.Playing.Value = true;
            input1.Update(InputDevice, SamplingFrequency.Hz48000, 0, 1);
            input2.Update(InputDevice, SamplingFrequency.Hz48000, 1, 1);
            WaitAndAssert(LifecyclePhase.Play, input1);
            WaitAndAssert(LifecyclePhase.Play, input2);
            output1.Update(input1.Output, OutputDevice, SamplingFrequency.Hz48000, 0, 1);
            WaitAndAssert(LifecyclePhase.Play, output1);
            output2.Update(input2.Output, OutputDevice, SamplingFrequency.Hz48000, 1, 1);
            WaitAndAssert(LifecyclePhase.Play, output2);


            Assert.AreSame(input1.Device.Device, input2.Device.Device);
            Assert.AreSame(output1.Device.Device, output2.Device.Device);

            Assert.AreEqual(512, input1.AudioFormat.BufferSize);
            Assert.AreEqual(512, input2.AudioFormat.BufferSize);
            var realFormat = input1.Device.Device.RecordingParams.AudioFormat;
            Assert.AreEqual(1024, realFormat.BufferSize);
            var inputSignal = BuildSignal(realFormat);
            input1.Device.OnDataReceived(MixBuffers.CopyFloatToByte(inputSignal));
            input1.Device.OnDataReceived(MixBuffers.CopyFloatToByte(inputSignal));
            Task.Delay(100).GetAwaiter().GetResult();

            Assert.AreEqual(512, output1.AudioFormat.BufferSize);
            Assert.AreEqual(512, output2.AudioFormat.BufferSize);
            var signal = new float[realFormat.BufferSize];
            // output1.Device.PlayingBuffer().Read(signal);
            AssertSignal(inputSignal, signal);
            Array.Clear(signal, 0, signal.Length);
            // output2.Device.PlayingBuffer().Read(signal);
            AssertSignal(inputSignal, signal);
        }
    }
}