using NewAudio.Processor;
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
        private MathProcessor _mathProcessor;
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
                    _mathProcessor = new MultiplyProcessor(new AudioProcessorConfig(){AutoEnable = Params.Enable.Value});
                }

                AudioProcessor = _mathProcessor;
            }

            if (Params.Value.HasChanged && _mathProcessor!=null )
            {
                Params.Value.Commit();
                _mathProcessor.Params.Value.Value = Params.Value.Value;
            }

            if (Params.Input.HasChanged && AudioProcessor!=null)
            {
                Params.Input.Commit();
                Params.Input.Value?.Pin.Connect(AudioProcessor);
            }

            if (Params.Enable.HasChanged && _mathProcessor != null)
            {
                Params.Enable.Commit();
                _mathProcessor.SetEnabled(Params.Enable.Value);
            }
            
            enabled = _mathProcessor?.IsEnabled ?? false;
            return Output;
        }
        
        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _mathProcessor.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}