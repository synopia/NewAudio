using System;
using System.Threading;
using NUnit.Framework;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudioTest.Sources
{
    [TestFixture]
    public class BufferingSourceTest
    {
        [Test]
        public void TestSimple()
        {
            var sine1 = new GeneratorSource();
            sine1.Amplitude = 1;
            sine1.Frequency = 1000;
            
            sine1.PrepareToPlay(44100, 4024);
            var buf = new AudioBuffer(2, 4024);
            var info = new AudioSourceChannelInfo(buf, 0, 4024);

            sine1.GetNextAudioBlock(info);
            var mem = new MemoryAudioSource(buf);

            var buffering = new BufferingAudioSource(mem, 512, 2);
            var b2 = new AudioBuffer(2, 300);
            var i2 = new AudioSourceChannelInfo(b2, 0, 300);
            
            buffering.PrepareToPlay(44100, 300);
            Assert.AreEqual(0, buffering.NextReadPos);
            
            buffering.GetNextAudioBlock(i2);
            var phase = 0;
            while (phase<3900)
            {
                phase = SineGenTest.AssertSine(phase, 2, 300, 1000, 1, b2);
                Assert.AreEqual(phase, buffering.NextReadPos);
                Thread.Sleep(10);
                buffering.GetNextAudioBlock(i2);                
            }
            phase = SineGenTest.AssertSine(phase, 2, 124, 1000, 1, b2);
            Assert.AreEqual(4024, phase);
            Assert.AreEqual(4200, buffering.NextReadPos);

        }
    }
}