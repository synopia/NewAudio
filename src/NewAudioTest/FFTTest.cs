using System.Threading;
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
            WaitHandle[] handles = {
                input.Lifecycle.WaitForEvents, output.Lifecycle.WaitForEvents, output2.Lifecycle.WaitForEvents,
                fft.Lifecycle.WaitForEvents
            }; 
            Assert.IsEmpty(input.ErrorMessages());
            Assert.IsEmpty(output.ErrorMessages());
            Assert.IsEmpty(output2.ErrorMessages());
            Assert.IsEmpty(fft.ErrorMessages());
            input.PlayParams.Playing.Value = true;
            output.PlayParams.Playing.Value = true;
            output2.PlayParams.Playing.Value = true;
            fft.PlayParams.Playing.Value = true;

            input.Update(inputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            fft.Update(input.Output, 1024);
            output2.Update(null,  outputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            output.Update(fft.Output, outputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            WaitHandle.WaitAll(handles);
            
            Assert.IsEmpty(fft.ErrorMessages());
            Assert.IsEmpty(output2.ErrorMessages());
            Assert.IsEmpty(output.ErrorMessages());

            Assert.AreEqual(LifecyclePhase.Play, fft.Phase);
            Assert.AreEqual(LifecyclePhase.Init, output2.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output.Phase);

            input.Update(inputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            WaitHandle.WaitAll(handles);
          
            fft.Update(input.Output, 1024);
            WaitHandle.WaitAll(handles);
            output.Update(null, outputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            WaitHandle.WaitAll(handles);
            output2.Update(fft.Output, outputNullEnum, SamplingFrequency.Hz48000, 0, 1);
            WaitHandle.WaitAll(handles);
            
            Assert.AreEqual(LifecyclePhase.Play, fft.Phase);
            Assert.AreEqual(LifecyclePhase.Init, output.Phase);
            Assert.AreEqual(LifecyclePhase.Play, output2.Phase);
        }
    }
}