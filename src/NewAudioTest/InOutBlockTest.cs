using System;
using System.Threading.Tasks;
using NewAudio.Core;
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

        private float[] BuildSignal(AudioFormat format, int offset = 0, bool interleaved = true)
        {
            var signal = new float[format.BufferSize];
            if (interleaved)
            {
                var pos = 0;
                for (var s = 0; s < format.SampleCount; s++)
                {
                    for (var ch = 0; ch < format.Channels; ch++)
                    {
                        signal[pos] = offset + ch;
                        pos++;
                    }
                }
            }
            else
            {
                var pos = 0;
                for (var ch = 0; ch < format.Channels; ch++)
                {
                    for (var s = 0; s < format.SampleCount; s++)
                    {
                        signal[pos] = ch;
                        pos++;
                    }
                }
            }

            return signal;
        }

        public void AssertSignal(float[] expected, float[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, "Length must match");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i], "Differs at {0}", i);
            }
        }

        [Test]
        public void TestRead2ChI_Write2ChI()
        {
            using var input = new InputDevice();
            using var output = new OutputDevice();

            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            input.Update(InputEnum);
            output.Update(input.Output, OutputEnum);
            WaitAndAssert(LifecyclePhase.Play, input, output);

            Assert.AreEqual(1024, input.AudioFormat.BufferSize);
            var inputSignal = BuildSignal(input.Output.Format);
            input.Device.RecordingBuffer().Write(inputSignal);
            Task.Delay(100).GetAwaiter().GetResult();

            Assert.AreEqual(1024, output.AudioFormat.BufferSize);
            var outputSignal = new float[output.AudioFormat.BufferSize];
            output.Device.PlayingBuffer().Read(outputSignal);

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
            input1.Update(InputEnum, SamplingFrequency.Hz48000, 0, 1);
            WaitAndAssert(LifecyclePhase.Play, input1);
            input2.Update(InputEnum, SamplingFrequency.Hz48000, 1, 1);
            WaitAndAssert(LifecyclePhase.Play, input2);
            output1.Update(input1.Output, OutputEnum, SamplingFrequency.Hz48000, 0, 1);
            WaitAndAssert(LifecyclePhase.Play, output1);
            output2.Update(input2.Output, OutputEnum, SamplingFrequency.Hz48000, 1, 1);
            WaitAndAssert(LifecyclePhase.Play, output2);


            Assert.AreSame(input1.Device.Device, input2.Device.Device);
            Assert.AreSame(output1.Device.Device, output2.Device.Device);

            Assert.AreEqual(512, input1.AudioFormat.BufferSize);
            Assert.AreEqual(512, input2.AudioFormat.BufferSize);
            var realFormat = input1.Device.Device.RecordingConfig.AudioFormat;
            Assert.AreEqual(1024, realFormat.BufferSize);
            var inputSignal = BuildSignal(realFormat);
            input1.Device.RecordingBuffer().Write(inputSignal);
            input1.Device.RecordingBuffer().Write(inputSignal);
            Task.Delay(100).GetAwaiter().GetResult();

            Assert.AreEqual(512, output1.AudioFormat.BufferSize);
            Assert.AreEqual(512, output2.AudioFormat.BufferSize);
            var signal = new float[realFormat.BufferSize];
            output1.Device.PlayingBuffer().Read(signal);
            AssertSignal(inputSignal, signal);
            Array.Clear(signal, 0, signal.Length);
            output2.Device.PlayingBuffer().Read(signal);
            AssertSignal(inputSignal, signal);
        }
    }
}