using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;


namespace NewAudio.Nodes
{
    public class AudioGeneratorParams : AudioParams
    {
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<bool> Interleaved;
        public AudioParam<int> SampleCount;
        public AudioParam<int> Channels;
        public AudioParam<int> DesiredLatency;
    }

    public class AudioGenerator : AudioNode
    {
        public override string NodeName => "Gen";
        private AudioGeneratorBlock _audioGeneratorBlock;
        private AudioFormat _format;
        public AudioGeneratorParams Params { get; }
        
        public AudioGenerator()
        {
            InitLogger<AudioGenerator>();
            Params = AudioParams.Create<AudioGeneratorParams>();
            Logger.Information("AudioGenerator created");
            _format = new AudioFormat(48000, 512);
        }

        public AudioLink Update(SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100, int sampleCount = 512,
            int channels = 2, int desiredLatency = 250, bool interleaved = true)
        {
            Params.SamplingFrequency.Value = samplingFrequency;
            Params.SampleCount.Value = sampleCount;
            Params.DesiredLatency.Value = desiredLatency;
            Params.Channels.Value = channels;
            Params.Interleaved.Value = interleaved;
            PlayConfig.Update(null, Params.HasChanged);
            
            if (Params.HasChanged)
            {
                PlayConfig.Reset.Value = true;
                _format = new AudioFormat((int)Params.SamplingFrequency.Value, Params.SampleCount.Value,
                    Params.Channels.Value, Params.Interleaved.Value);
                Output.Format = _format;    
            }

            return base.Update(Params);
        }

        public override bool Play()
        {
            if (Params.SampleCount.Value > 0 && Params.Channels.Value > 0)
            {
                _audioGeneratorBlock = new AudioGeneratorBlock();
                _format = new AudioFormat((int)Params.SamplingFrequency.Value, Params.SampleCount.Value,
                    Params.Channels.Value, Params.Interleaved.Value);
                Output.Format = _format;
                var buffer = new BufferBlock<AudioDataMessage>(new DataflowBlockOptions()
                {
                    BoundedCapacity = 1
                });
                _audioGeneratorBlock.Create(buffer, _format);
                Output.SourceBlock = buffer;
                return true;
            }

            return false;
        }

        public override void Stop()
        {
            _audioGeneratorBlock?.Stop();
            _audioGeneratorBlock = null;
            _format = default;
            Output.SourceBlock = null;
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _audioGeneratorBlock.Dispose();
                    _audioGeneratorBlock = null;
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

        public override string DebugInfo()
        {
            return $"[{this}, {base.DebugInfo()}]";
        }
    }
}