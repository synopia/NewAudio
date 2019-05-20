using System.Threading;
using NUnit.Framework;
using VL.NewAudio;

namespace NewAudioTest
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void TestThreads()
        {
            var a = new AudioThread();
            var latency = 10;
            var cpuUsage = 1.0f;
            var underRuns = 1;

            a.Update(null, out latency, out cpuUsage, out underRuns, true);
            Thread.Sleep(200);
            a.Dispose();
        }
        [Test]
        public void TestArrayEquals()
        {
            var f1 = LinearArray(0, 100);
            var f2 = LinearArray(0, 100);
            var f3 = LinearArray(1, 100);
            Assert.IsTrue(AudioEngine.ArrayEquals(f1, f2));
            Assert.IsTrue(!AudioEngine.ArrayEquals(f1, f3));

            var asb1 = new AudioSampleBuffer(WaveOutput.InternalFormat);
            var asb2 = new AudioSampleBuffer(WaveOutput.InternalFormat);

            var f4 = new[] {asb1, asb2};
            var f5 = new[] {asb1, asb2};
            var f6 = new[] {asb2, asb1};
            var f7 = new[] {asb1, null};
            var f8 = new[] {asb1, null};
            Assert.IsTrue(AudioEngine.ArrayEquals(f5, f4));
            Assert.IsTrue(!AudioEngine.ArrayEquals(f5, f6));
            Assert.IsTrue(!AudioEngine.ArrayEquals(f5, f7));
            Assert.IsTrue(AudioEngine.ArrayEquals(f8, f7));
        }

        [Test]
        public void TestCircularSampleBuffer()
        {
            var x = new CircularSampleBuffer(4000);
            Assert.AreEqual(0, x.Count);

            x.Write(LinearArray(0, 3950), 0, 3950);
            Assert.AreEqual(3950, x.Count);

            var target = new float[3950];
            x.Read(target, 0, 3950);
            Assert.AreEqual(0, x.Count);

            var source = LinearArray(0, 100);
            x.Write(source, 0, 100);
            Assert.AreEqual(100, x.Count);

            target = new float[100];
            x.Read(target, 0, 100);
            Assert.AreEqual(0, x.Count);
            Assert.AreEqual(source, target);

            var read = x.Read(target, 0, 100);
            Assert.AreEqual(0, read);
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