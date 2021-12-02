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

        [Test]
        public void Limit()
        {
            var mono = AudioChannels.Mono;
            var stereo = AudioChannels.Stereo;
            Assert.AreEqual(AudioChannels.Mono, mono.Limit(1));
            Assert.AreEqual(AudioChannels.Mono, stereo.Limit(1));
            Assert.AreEqual(AudioChannels.Stereo, stereo.Limit(2));
            Assert.AreEqual(AudioChannels.Stereo, AudioChannels.Channels(10).Limit(2));
            Assert.AreEqual(4, AudioChannels.Channels(10).Limit(4).Count);
        }
    }
}