using NUnit.Framework;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudioTest.Sources
{
    [TestFixture]
    public class MixerTest
    {
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
            var phase = SineGenTest.AssertSine(0, 2,724,1000,1,info.Buffer);
            
            var mixer = new MixerSource();
            mixer.Sources = new[] { sine1 };
            info.ClearActiveBuffer();
            mixer.GetNextAudioBlock(info);
            phase = SineGenTest.AssertSine(phase, 2,724,1000,1,info.Buffer);
            
        }
        [Test]
        public void Simple2Sines()
        {
            var sine1 = new GeneratorSource();
            sine1.Amplitude = 1;
            sine1.Frequency = 1000;
            var sine2 = new GeneratorSource();
            sine2.Amplitude = 1;
            sine2.Frequency = 1000;
            
            sine1.PrepareToPlay(44100, 1024);
            sine2.PrepareToPlay(44100, 1024);
            
            var buf = new AudioBuffer(2, 724);
            var info = new AudioSourceChannelInfo(buf, 0, 724);
            var mixer = new MixerSource();
            mixer.Sources = new[] { sine1, sine2 };
            
            mixer.GetNextAudioBlock(info);
            var phase = SineGenTest.AssertSine(0, 2,724,1000,2,info.Buffer);
            sine2.Amplitude = 0.5f;
            
            info.ClearActiveBuffer();
            mixer.GetNextAudioBlock(info);
            phase = SineGenTest.AssertSine(phase, 2,724,1000,1.5f,info.Buffer);
            
        }
    }
}