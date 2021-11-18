using NewAudio.Core;
using NewAudio.Dsp;

namespace NewAudio.Block
{
    public class MathBlockParams : AudioParams
    {
        public AudioParam<float> Value;
    }
    
    public abstract class MathBlock: AudioBlock
    {
        public readonly MathBlockParams Params;
        
        public MathBlock(AudioBlockFormat format) : base(format)
        {
            InitLogger<MathBlock>();
            Params = AudioParams.Create<MathBlockParams>();
        }
    }

    public class MultiplyBlock : MathBlock
    {
        public override string Name => "*";

        public MultiplyBlock(AudioBlockFormat format) : base(format)
        {
        }

        protected override void Process(AudioBuffer buffer)
        {
            Dsp.Dsp.Mul(buffer.Data, Params.Value.Value, buffer.Data, buffer.Size);
        }
    }
}