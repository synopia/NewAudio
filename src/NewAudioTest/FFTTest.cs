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
            var fft = new ForwardFFT();
            Assert.IsNull(input.ErrorMessages());
            Assert.IsNull(output.ErrorMessages());
            Assert.IsNull(fft.ErrorMessages());
            
            input.Update(inputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            fft.Update(input.Output, 1024);
            output.Update(fft.Output, outputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            Assert.IsNull(fft.ErrorMessages());

            Assert.AreEqual(LifecyclePhase.Ready, fft.Phase);
            
        }
    }
}