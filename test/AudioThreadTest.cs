using System.Threading;
using NUnit.Framework;
using VL.NewAudio;

namespace NewAudioTest
{
    public class AudioThreadTest
    {
        [Test]
        public void TestThreads()
        {
            var a = new AudioThread();
            var latency = 10;
            var cpuUsage = 1.0f;
            var underRuns = 1;

            var output = a.Update(TestHelper.Silence(1), out latency, out cpuUsage, out underRuns, false);
            float[] buf = new float[256];
            Thread.Sleep(200);
            Assert.IsNotNull(output);
            Assert.IsTrue(a.PlayBuffer.BufferedSamples>0);
            output.Read(buf, 0, buf.Length);
            Assert.AreEqual(TestHelper.GenerateBuffer(new[] {1.0f}, 256), buf);
            
            output = a.Update(null, out latency, out cpuUsage, out underRuns, false);
            Thread.Sleep(200);
            Assert.IsNotNull(output);
            Assert.IsTrue(a.PlayBuffer.BufferedSamples==0);
            output.Read(buf, 0, buf.Length);
            Assert.AreEqual(TestHelper.GenerateBuffer(new[] {0.0f}, 256), buf);

            var input = TestHelper.Silence(2);
            output = a.Update(input, out latency, out cpuUsage, out underRuns, true);
            Thread.Sleep(200);
            Assert.IsNull(output);
            Assert.IsTrue(a.PlayBuffer.BufferedSamples>0);
            Assert.IsTrue(((LevelProcessor)input.Processor).ReadPos>0);

            Thread.Sleep(200);
            a.Dispose();
        }
    }
}