using VL.NewAudio.Core;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Sources
{
    public class GeneratorSource : AudioSourceNode
    {
        private float _phase;
        private float _period;
        private int _sampleRate;

        public float Amplitude { get; set; } = 0.5f;
        public float Frequency { get; set; } = 1000f;

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            _period = 1.0f / sampleRate;
            _phase = 0;
            _sampleRate = sampleRate;
        }

        public override void ReleaseResources()
        {
        }

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            var increase = Frequency * _period;
            for (var i = 0; i < bufferToFill.NumFrames; i++)
            {
                var sample = Amplitude * AudioMath.SinF(_phase * AudioMath.TwoPi);
                _phase = AudioMath.Fract(_phase + increase);
                for (var ch = 0; ch < bufferToFill.Buffer.NumberOfChannels; ch++)
                {
                    bufferToFill.Buffer[ch, bufferToFill.StartFrame + i] = sample;
                }
            }
        }
    }
}