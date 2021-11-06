using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class FFTTest
    {
        [Test]
        public void TestIt()
        {
            AudioService.Instance.Init();
            var inputNullEnum = new WaveInputDevice("Null: Input");
            var outputNullEnum = new WaveOutputDevice("Null: Output");
            var input = new InputDevice();
            var output = new OutputDevice();
            var output2 = new OutputDevice();
            var fft = new ForwardFFT();
            Assert.IsNull(input.ErrorMessages());
            Assert.IsNull(output.ErrorMessages());
            Assert.IsNull(output2.ErrorMessages());
            Assert.IsNull(fft.ErrorMessages());
            
            input.Update(inputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            fft.Update(input.Output, 1024);
            output2.Update(null,  outputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            output.Update(fft.Output, outputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            Assert.IsNull(fft.ErrorMessages());
            Assert.IsNull(output2.ErrorMessages());
            Assert.IsNull(output.ErrorMessages());

            Assert.AreEqual(LifecyclePhase.Created, fft.Phase);
            Assert.AreEqual(LifecyclePhase.Invalid, output2.Phase);
            Assert.AreEqual(LifecyclePhase.Created, output.Phase);

            Task.Delay(10).GetAwaiter().GetResult();
            input.Update(inputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            fft.Update(input.Output, 1024);
            output2.Update(fft.Output, outputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            output.Update(null, outputNullEnum, SamplingFrequency.Hz48000, 0, 1);

            Assert.AreEqual(LifecyclePhase.Created, fft.Phase);
            Assert.AreEqual(LifecyclePhase.Invalid, output.Phase);
            Assert.AreEqual(LifecyclePhase.Created, output2.Phase);
        }
    }
}