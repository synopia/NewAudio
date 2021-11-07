using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class AudioBufferOutTest
    {
        [Test]
        public void TestIt()
        {
            var buf = new AudioBufferOut();
            var inputNullEnum = new WaveInputDevice("Null: Input");
            var input = new InputDevice();
            input.Update(inputNullEnum, SamplingFrequency.Hz48000, 0, 1);

            input.PlayParams.Playing.Value = true;
            buf.PlayParams.Playing.Value = true;

            var spread = buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);
            buf.Lifecycle.WaitForEvents.WaitOne();
            Assert.IsEmpty(buf.ErrorMessages());
            Assert.AreEqual(LifecyclePhase.Play, buf.Phase);
            Assert.AreEqual(1024, spread.Count);
            
        }
    }
}