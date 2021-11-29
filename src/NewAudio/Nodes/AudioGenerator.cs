using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Processor;
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
        private GenProcessor _genProcessor;
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
            
            if (Params.GeneratorType.HasChanged )
            {
                Params.GeneratorType.Commit();
                if (type == GeneratorType.Noise)
                {
                    _genProcessor = new NoiseGenProcessor(new AudioProcessorConfig(){AutoEnable = enable});
                } else if (type == GeneratorType.Sine)
                {
                    _genProcessor = new SineGenProcessor(new AudioProcessorConfig(){AutoEnable = enable});
                }

                _genProcessor.Params.Freq = Params.Freq;
                AudioProcessor = _genProcessor;
                
            }

            // if (Params.Freq.HasChanged && _genBlock!=null )
            // {
                // Params.Freq.Commit();
                // _genBlock.Params.Freq.Value = Params.Freq.Value;
            // }

            if (Params.Enable.HasChanged && _genProcessor!=null)
            {
                Params.Enable.Commit();
                _genProcessor.SetEnabled(Params.Enable.Value);
            }
            
            enabled = _genProcessor?.IsEnabled ?? false;
            return Output;
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _genProcessor.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}