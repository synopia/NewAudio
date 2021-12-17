using NUnit.Framework;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudioTest.Sources
{
    [TestFixture]
    public class RouterTest
    {
        [Test]
        public void TestOverwriteInput()
        {
            var sine1 = new GeneratorSource();
            sine1.Amplitude = 1;
            sine1.Frequency = 1000;

            sine1.PrepareToPlay(44100, 1024);
            var buf = new AudioBuffer(2, 724);
            var info = new AudioSourceChannelInfo(buf, 0, 724);
            sine1.Amplitude = 10;
            sine1.GetNextAudioBlock(info);
            sine1.PrepareToPlay(44100, 1024);

            var router = new ChannelRouterSource();
            router.Source = sine1;
            router.InputMap = new[] {0, 1};
            router.OutputMap = new[] {0, 1};

            sine1.Amplitude = 1;

            router.GetNextAudioBlock(info);
            Assert.AreEqual(2, info.Buffer.NumberOfChannels);
            var phase = SineGenTest.AssertSine(0, 2, 724, 1000, 1, info.Buffer);
        }

        [Test]
        public void TestDuplicateChannels()
        {
            var sine1 = new GeneratorSource();
            sine1.Amplitude = 1;
            sine1.Frequency = 1000;

            sine1.PrepareToPlay(44100, 1024);
            var buf = new AudioBuffer(4, 724);
            var info = new AudioSourceChannelInfo(buf, 0, 724);
            sine1.Amplitude = 10;
            sine1.GetNextAudioBlock(info);
            sine1.PrepareToPlay(44100, 1024);
            sine1.Amplitude = 1;

            var router = new ChannelRouterSource();
            router.NumberOfChannelsToProduce = 2;
            router.Source = sine1;
            router.InputMap = new[] { 0, 1 };
            router.OutputMap =new[] { 2, 3 };

            router.GetNextAudioBlock(info);
            Assert.AreEqual(4, info.Buffer.NumberOfChannels);
            SineGenTest.AssertSine(0, 2, 724, 1000, 0, info.Buffer[0].AsSpan(724));
            SineGenTest.AssertSine(0, 2, 724, 1000, 0, info.Buffer[1].AsSpan(724));
            SineGenTest.AssertSine(0, 2, 724, 1000, 1, info.Buffer[2].AsSpan(724));
            SineGenTest.AssertSine(0, 2, 724, 1000, 1, info.Buffer[3].AsSpan(724));
        }
   
    }
}