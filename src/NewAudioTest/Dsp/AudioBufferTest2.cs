using NewAudio.Dsp;
using NUnit.Framework;

namespace NewAudioTest.Dsp
{
    [TestFixture]
    public class AudioBufferTest2
    {
        [Test]
        public void TestCopy()
        {
            using var b = new AudioBuffer(2, 1024);
            int count = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < 1024; i++)
                {
                    b[ch, i] = count++;
                }
            }

            using var c = new AudioBuffer(b.GetWriteChannels(/*1*/), 1, 1024);
            int j = 0;
            count = 1024;
            for (int i = 0; i < 1024; i++)
            {
                c[j, i] = count++;
            }
            count = 1;
            for (int i = 0; i < 1024; i++)
            {
                c[j, i] = count;
            }
            for (int ch = 0; ch < 2; ch++)
            {
                count = 1;
                for (int i = 0; i < 1024; i++)
                {
                    b[ch, i] = count++;
                }
            }
        }

        [Test]
        public void TestGetSet()
        {
            using var b = new AudioBuffer(2, 1024);
            Assert.AreEqual(2*1024, b.Size);
            int count = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < 1024; i++)
                {
                    b[ch, i] = count++;
                }
            }
            using var c = new AudioBuffer(b);
            count = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < 1024; i++)
                {
                    Assert.AreEqual((float)count, b[ch,i]);
                    Assert.AreEqual((float)count++, c[ch,i]);
                }
            }
            
            
        }
    }
}