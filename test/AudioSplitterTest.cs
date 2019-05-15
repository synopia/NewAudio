using NUnit.Framework;
using VL.Lib.Collections;
using VL.NewAudio;

namespace NewAudioTest
{
    [TestFixture]
    public class AudioSplitterTest
    {
        [Test]
        public void TestSplitter()
        {
            var splitter = new AudioSplitter();
            var mixer = new AudioMixer();

            var spread = new[] {Silence(1), Silence(2), Silence(3)}.ToSpread();
            var ch3 = mixer.Update(spread, null);
            Spread<AudioSampleBuffer> output = splitter.Update(ch3, new[] {2, 1}.ToSpread());

            Assert.AreEqual(2, output[0].WaveFormat.Channels);
            Assert.AreEqual(1, output[1].WaveFormat.Channels);

            float[] buf = new float[256];
            output[0].Read(buf, 0, 256);
            Assert.AreEqual(GenerateBuffer(new[] {1.0f, 2.0f}, 256), buf);
            output[0].Read(buf, 0, 256);
            Assert.AreEqual(GenerateBuffer(new[] {1.0f, 2.0f}, 256), buf);
            output[0].Read(buf, 0, 256);
            Assert.AreEqual(GenerateBuffer(new[] {1.0f, 2.0f}, 256), buf);
        }

        private float[] GenerateBuffer(float[] levels, int len)
        {
            float[] buf = new float[len];
            for (int i = 0; i < len; i++)
            {
                buf[i] = levels[i % levels.Length];
            }

            return buf;
        }

        private AudioSampleBuffer Silence(float level)
        {
            var buf = new AudioSampleBuffer(WaveOutput.SingleChannelFormat);
            buf.Update = (b, o, len) =>
            {
                for (int i = 0; i < len; i++)
                {
                    b[i] = level;
                }

                return len;
            };
            return buf;
        }
    }
}