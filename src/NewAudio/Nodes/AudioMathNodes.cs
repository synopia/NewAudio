using NewAudio.Block;
using NewAudio.Core;

namespace NewAudio.Nodes
{
    public enum MathOperation
    {
        Multiply
    }
    public class AudioMathParams : MathBlockParams
    {
        public AudioParam<AudioLink> Input;
        public AudioParam<bool> Enable;
        public AudioParam<MathOperation> Operation;
    }
    
    public class AudioMathNode: AudioNode
    {
        public override string NodeName => "AudioMath";
        private MathBlock _mathBlock;
        public AudioMathParams Params;

        public AudioMathNode()
        {
            InitLogger<AudioMathNode>();
            Params = AudioParams.Create<AudioMathParams>();
        }

        public AudioLink Update(bool enable, AudioLink input, float value, MathOperation operation, out bool enabled)
        {
            Params.Input.Value = input;
            Params.Operation.Value = operation;
            Params.Value.Value = value;
            Params.Enable.Value = enable;

            if (Params.Operation.HasChanged)
            {
                Params.Operation.Commit();

                if (operation == MathOperation.Multiply)
                {
                    _mathBlock = new MultiplyBlock(new AudioBlockFormat(){AutoEnable = Params.Enable.Value});
                }

                AudioBlock = _mathBlock;
            }

            if (Params.Value.HasChanged && _mathBlock!=null )
            {
                Params.Value.Commit();
                _mathBlock.Params.Value.Value = Params.Value.Value;
            }

            if (Params.Input.HasChanged && AudioBlock!=null)
            {
                Params.Input.Commit();
                Params.Input.Value?.Pin.Connect(AudioBlock);
            }

            if (Params.Enable.HasChanged && _mathBlock != null)
            {
                Params.Enable.Commit();
                _mathBlock.SetEnabled(Params.Enable.Value);
            }
            
            enabled = _mathBlock?.IsEnabled ?? false;
            return Output;
        }
        
        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _mathBlock.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}