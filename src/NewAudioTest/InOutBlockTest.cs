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
        [Test]
        public void TestRead2ChI_Write2ChI()
        {
            using var input = new InputDevice();
            using var output = new OutputDevice();

            input.PlayConfig.Phase.Value = LifecyclePhase.Play;
            output.PlayConfig.Phase.Value = LifecyclePhase.Play;
            input.Update(InputDevice);
            output.Update(input.Output, OutputDevice);
            UpdateDevices();
            input.Update(InputDevice);
            output.Update(input.Output, OutputDevice);
            input.Update(InputDevice);
            output.Update(input.Output, OutputDevice);
            DriverManager.Resource.UpdateAllDevices();
            output.ActualDeviceParams.Update();
            Assert.AreEqual(1024, output.AudioFormat.BufferSize);

            Assert.AreEqual(1024, input.AudioFormat.BufferSize);
            var inputSignal = BuildSignal(input.Output.Format);
            input.Device.OnDataReceived(MixBuffers.CopyFloatToByte(inputSignal));
            var outputSignal = output.Device.GetReadBuffer().GetFloatArray(); // reads first, empty buffer
            input.Device.OnDataReceived(MixBuffers.CopyFloatToByte(inputSignal));
            outputSignal = output.Device.GetReadBuffer().GetFloatArray();
            
            AssertSignal(inputSignal, outputSignal);
        }

        [Test]
        public void TestRead2x1ChI_Write2x1Ch()
        {
            using var input1 = new InputDevice();
            using var input2 = new InputDevice();
            using var output1 = new OutputDevice();
            using var output2 = new OutputDevice();

            input1.PlayConfig.Phase.Value = LifecyclePhase.Play;
            input2.PlayConfig.Phase.Value = LifecyclePhase.Play;
            output1.PlayConfig.Phase.Value = LifecyclePhase.Play;
            output2.PlayConfig.Phase.Value = LifecyclePhase.Play;
            input1.Update(InputDevice, SamplingFrequency.Hz48000, 0, 1);
            input2.Update(InputDevice, SamplingFrequency.Hz48000, 1, 1);
            output1.Update(input1.Output, OutputDevice, SamplingFrequency.Hz48000, 0, 1);
            output2.Update(input2.Output, OutputDevice, SamplingFrequency.Hz48000, 1, 1);
            UpdateDevices();


            Assert.AreSame(input1.Device.Device, input2.Device.Device);
            Assert.AreSame(output1.Device.Device, output2.Device.Device);

            Assert.AreEqual(512, input1.AudioFormat.BufferSize);
            Assert.AreEqual(512, input2.AudioFormat.BufferSize);
            var realFormat = input1.Device.Device.RecordingParams.AudioFormat;
            Assert.AreEqual(1024, realFormat.BufferSize);
            Assert.AreEqual(512, output1.AudioFormat.BufferSize);
            Assert.AreEqual(512, output2.AudioFormat.BufferSize);

            var inputSignal = BuildSignal(realFormat);
            
            input1.Device.OnDataReceived(MixBuffers.CopyFloatToByte(inputSignal));
            var outputSignal = output1.Device.GetReadBuffer().GetFloatArray(); // reads first, empty buffer
            input1.Device.OnDataReceived(MixBuffers.CopyFloatToByte(inputSignal));
            outputSignal = output1.Device.GetReadBuffer().GetFloatArray();
            
            AssertSignal(inputSignal, outputSignal);
        }
    }
}