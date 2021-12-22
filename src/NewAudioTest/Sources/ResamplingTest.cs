using NUnit.Framework;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudioTest.Sources
{
    [TestFixture]
    public class ResamplingTest
    {
        [Test]
        public void ResamplingTest1()
        {
            var sine1 = new GeneratorSource();
            sine1.Amplitude = 1;
            sine1.Frequency = 1000;
            
            // sine1.PrepareToPlay(44100, 512);

            var resampling = new ResamplingAudioSource(sine1, 22050, 2);
            resampling.PrepareToPlay(44100, 512);
            
            var buf = new AudioBuffer(2, 512);
            var info = new AudioBufferToFill(buf, 0, 512);

            resampling.FillNextBuffer(info);
            resampling.FillNextBuffer(info);
            resampling.FillNextBuffer(info);
            resampling.FillNextBuffer(info);
            resampling.FillNextBuffer(info);
            resampling.FillNextBuffer(info);
            resampling.FillNextBuffer(info);
            resampling.FillNextBuffer(info);
            resampling.FillNextBuffer(info);
        }
    }
}