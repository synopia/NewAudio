using NUnit.Framework;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudioTest.Sources
{
    [TestFixture]
    public class MemorySourceTest
    {
        [Test]
        public void TestSimple()
        {
            var sine1 = new GeneratorSource();
            sine1.Amplitude = 1;
            sine1.Frequency = 1000;
            
            sine1.PrepareToPlay(44100, 1024);
            var buf = new AudioBuffer(2, 1024);
            var info = new AudioSourceChannelInfo(buf, 0, 1024);

            sine1.GetNextAudioBlock(info);

            var mem = new MemoryAudioSource(buf);

            var b2 = new AudioBuffer(2, 102);
            var i2 = new AudioSourceChannelInfo(b2, 0, 102);
            Assert.AreEqual(0, mem.NextReadPos);
            mem.GetNextAudioBlock(i2);
            var phase = 0;
            while (phase+b2.Size<1024)
            {
                phase = SineGenTest.AssertSine(phase, 2, 102, 1000, 1, b2);
                Assert.AreEqual(phase, mem.NextReadPos);
                mem.GetNextAudioBlock(i2);                
            }
            phase = SineGenTest.AssertSine(phase, 2, 102, 1000, 1, b2);
            mem.GetNextAudioBlock(i2);                
            phase = SineGenTest.AssertSine(phase, 2, 1024-phase, 1000, 1, b2);
            Assert.AreEqual(1024, phase);
            Assert.AreEqual(1024, mem.NextReadPos);
            
            // phase = SineGenTest.AssertSine(0, 2, 2048, 1000, 1, b2);
            
            
        }
    }
}