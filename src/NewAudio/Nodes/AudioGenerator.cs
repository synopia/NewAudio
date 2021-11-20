using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Block;
using NewAudio.Core;


namespace NewAudio.Nodes
{
    public enum GeneratorType
    {
        Noise,
        Sine
    }
    public class AudioGeneratorParams : GenBlockParams
    {
        public AudioParam<bool> Enable;
        public AudioParam<GeneratorType> GeneratorType;
    }

    public class AudioGenerator : AudioNode
    {
        public override string NodeName => "AudioGenerator";
        private GenBlock _genBlock;
        public AudioGeneratorParams Params { get; }
        
        public AudioGenerator()
        {
            InitLogger<AudioGenerator>();
            Params = AudioParams.Create<AudioGeneratorParams>();
        }

        public AudioLink Update(bool enable, float frequency, GeneratorType type, out bool enabled)
        {
            Params.GeneratorType.Value = type;
            Params.Freq.Value = frequency;
            Params.Enable.Value = enable;
            
            if (Params.GeneratorType.HasChanged || _genBlock==null)
            {
                if (_genBlock != null)
                {
                    _genBlock.Dispose();
                }

                if (type == GeneratorType.Noise)
                {
                    _genBlock = new NoiseGenBlock(new AudioBlockFormat(){AutoEnable = true});
                } else if (type == GeneratorType.Sine)
                {
                    _genBlock = new SineGenBlock(new AudioBlockFormat(){AutoEnable = true});
                    
                }

                AudioBlock = _genBlock;
            }

            if (Params.Freq.HasChanged && _genBlock!=null )
            {
                _genBlock.Params.Freq.Value = Params.Freq.Value;
            }

            if (Params.HasChanged)
            {
                Params.Commit();
                _genBlock?.SetEnabled(Params.Enable.Value);
            }
            
            enabled = _genBlock?.IsEnabled ?? false;
            return Output;
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _genBlock.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}