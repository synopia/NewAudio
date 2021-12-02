using NewAudio.Core;
using NewAudio.Dsp;

namespace NewAudio.Processor
{
    
    public abstract class MathProcessor: AudioProcessor
    {
        public float Value;
        
        protected MathProcessor()
        {
        }
        protected abstract float NextSample(float input);

        public override void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
        }
        public override void Process(AudioBuffer buffer)
        {
            for (int i = 0; i < buffer.NumberOfFrames; i++)
            {
                var channel = 0;
                for (int ch = 0; ch < TotalNumberOfOutputChannels; ch++)
                {
                    var sample = NextSample(buffer[channel,i]);
                    buffer[ch, i] = sample;

                    channel++;
                    channel %= TotalNumberOfInputChannels;
                }
            }
        }

        public override void ReleaseResources()
        {
            
        }

    }

    public class MultiplyProcessor : MathProcessor
    {
        public override string Name => "*";

        public MultiplyProcessor()
        {
        }

        protected override float NextSample(float input)
        {
            return Value * input;
        }
    }
}