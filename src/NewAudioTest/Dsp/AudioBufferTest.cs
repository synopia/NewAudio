using NewAudio.Dsp;
using NUnit.Framework;

namespace NewAudioTest.Dsp
{
    [TestFixture]
    public class AudioBufferTest
    {
        [Test]
        public void TestBuffer()
        {
            var buf = new AudioBuffer(512, 2)
            {
                [0] = 100
            };
            Assert.AreEqual(100, buf[0]);
            for (int i = 0; i < 512 * 2; i++)
            {
                buf[i] = i;
            }
            buf.Zero();
            for (int i = 0; i < 512 * 2; i++)
            {
                Assert.AreEqual(0, buf[i]);
            }
            for (int i = 0; i < 512 * 2; i++)
            {
                buf[i] = i;
            }
            var fs = buf.GetChannel(0);
            for (var i = 0; i < 512; i++)
            {
                Assert.AreEqual(i, fs[i]);
            }
            fs = buf.GetChannel(1);
            for (var i = 0; i < 512; i++)
            {
                Assert.AreEqual(512+i, fs[i]);
            }
        }
    }
}