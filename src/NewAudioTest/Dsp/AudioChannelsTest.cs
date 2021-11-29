using NewAudio.Dsp;
using NUnit.Framework;

namespace NewAudioTest.Dsp
{
    [TestFixture]
    public class AudioChannelsTest
    {
        [Test]
        public void Test()
        {
            var mono = AudioChannels.Mono;
            var stereo = AudioChannels.Stereo;
            
            Assert.AreEqual(1, mono.Count);
            Assert.AreEqual(1, mono.Mask);
            Assert.AreEqual(2, stereo.Count);
            Assert.AreEqual(1+2, stereo.Mask);
        }
    }
}