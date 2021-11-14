using System.Threading;
using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class FFTTest : BaseDeviceTest
    {
        [Test]
        public void TestIt()
        {
            using var input = new InputDevice();
            using var output = new OutputDevice();
            using var output2 = new OutputDevice();
            using var fft = new ForwardFFT();
            Assert.IsEmpty(input.ErrorMessages());
            Assert.IsEmpty(output.ErrorMessages());
            Assert.IsEmpty(output2.ErrorMessages());
            Assert.IsEmpty(fft.ErrorMessages());
            input.PlayParams.Phase.Value = LifecyclePhase.Play;
            output.PlayParams.Phase.Value = LifecyclePhase.Play;
            output2.PlayParams.Phase.Value = LifecyclePhase.Play;
            fft.PlayParams.Phase.Value = LifecyclePhase.Play;

            input.Update(InputDevice, SamplingFrequency.Hz48000, 0, 1);
            input.Update(InputDevice, SamplingFrequency.Hz48000, 0, 1);
            fft.Update(input.Output, 1024);
            output2.Update(null, OutputDevice, SamplingFrequency.Hz48000, 0, 1);
            output.Update(fft.Output, OutputDevice, SamplingFrequency.Hz48000, 0, 1);
            UpdateDevices();

            Assert.IsEmpty(fft.ErrorMessages());
            Assert.IsEmpty(output2.ErrorMessages());
            Assert.IsEmpty(output.ErrorMessages());

            Assert.AreEqual(LifecyclePhase.Play, fft.Phase);
            Assert.AreEqual(LifecyclePhase.Stop, output2.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);

            input.Update(InputDevice, SamplingFrequency.Hz48000, 0, 1);

            fft.Update(input.Output, 1024);
            output.Update(null, OutputDevice, SamplingFrequency.Hz48000, 0, 1);
            output2.Update(fft.Output, OutputDevice, SamplingFrequency.Hz48000, 0, 1);

            Assert.AreEqual(LifecyclePhase.Play, fft.Phase);
            Assert.AreEqual(LifecyclePhase.Stop, output.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output2.Phase);
        }
    }
}