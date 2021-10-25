using System.Threading;
using NewAudio;
using NewAudio.Internal;
using NUnit.Framework;
using VL.NewAudio;

namespace NewAudioTest
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void TestEnums()
        {
            Assert.AreEqual(44100, (int)AudioSampleRate.Hz44100);
        }
        [Test]
        public void TestArrayEquals()
        {
            var f1 = LinearArray(0, 100);
            var f2 = LinearArray(0, 100);
            var f3 = LinearArray(1, 100);
            Assert.IsTrue(Utils.ArrayEquals(f1, f2));
            Assert.IsTrue(!Utils.ArrayEquals(f1, f3));
        }

        [Test]
        public void TestCircularSampleBuffer()
        {
            var x = new CircularSampleBuffer("CSB", 4000);
            Assert.AreEqual(0, x.Count);

            x.Write(LinearArray(0, 3950), 0, 3950);
            Assert.AreEqual(3950, x.Count);

            var target = new float[3950];
            x.Read(target, 0, 3950);
            Assert.AreEqual(0, x.Count);

            var source = LinearArray(0, 100);
            x.Write(source, 0, 100);
            Assert.AreEqual(100, x.Count);

            target = new float[51];
            x.Read(target, 0, 49);
            Assert.AreEqual(51, x.Count);

            var read = x.Read(target, 0, 53);
            Assert.AreEqual(51, read);
            Assert.AreEqual(0, x.Count);
        }

        private static float[] LinearArray(int offset, int len)
        {
            float[] result = new float[len];
            for (int i = 0; i < len; i++)
            {
                result[i] = i + offset;
            }

            return result;
        }
    }
}