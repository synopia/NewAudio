using System;
using NUnit.Framework;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudioTest.Sources
{
    [TestFixture]
    public class SineGenTest
    {
        public static int AssertSine(int phase, int channels, int frames, float freq, float gain, Span<float> buf)
        {
            for (int i = 0; i < frames; i++)
            {
                var expected = AudioMath.SinF(freq * (i+phase) / 44100.0f * AudioMath.TwoPi) * gain;
                Assert.AreEqual(expected, buf[i], 1 / 1000f);
            }

            return phase + frames;
        }
        public static int AssertSine(int phase, int channels, int frames, float freq, float gain, AudioBuffer buf)
        {
            for (int i = 0; i < frames; i++)
            {
                var expected = AudioMath.SinF(freq * (i+phase) / 44100.0f * AudioMath.TwoPi) * gain;
                for (int ch = 0; ch < channels; ch++)
                {
                    Assert.AreEqual(expected, buf[ch, i], 1 / 1000f);
                }
            }

            return phase + frames;
        }

        [Test]
        public void SimpleTest()
        {
            var sine1 = new GeneratorSource();
            sine1.Amplitude = 1;
            sine1.Frequency = 1000;

            sine1.PrepareToPlay(44100, 1024);
            var buf = new AudioBuffer(2, 724);
            var info = new AudioSourceChannelInfo(buf, 0, 724);

            sine1.GetNextAudioBlock(info);
            var phase = AssertSine(0, 2, 724, 1000, 1, info.Buffer);

            info.Buffer.Zero();
            sine1.GetNextAudioBlock(info);
            phase = AssertSine(phase,2, 724, 1000, 1, info.Buffer);
            
        }
    }
}