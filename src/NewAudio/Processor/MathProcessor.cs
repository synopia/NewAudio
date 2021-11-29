using NewAudio.Core;
using NewAudio.Dsp;

namespace NewAudio.Processor
{
    public class MathBlockParams : AudioParams
    {
        public AudioParam<float> Value;
    }
    
    public abstract class MathProcessor: AudioProcessor
    {
        public readonly MathBlockParams Params;
        
        public MathProcessor(AudioProcessorConfig format) : base(format)
        {
            InitLogger<MathProcessor>();
            Params = AudioParams.Create<MathBlockParams>();
        }
    }

    public class MultiplyProcessor : MathProcessor
    {
        public override string Name => "*";

        public MultiplyProcessor(AudioProcessorConfig format) : base(format)
        {
        }

        protected override void Process(AudioBuffer buffer, int numFrames)
        {
            Dsp.Dsp.Mul(buffer.Data, Params.Value.Value, buffer.Data, numFrames);
        }
    }
}