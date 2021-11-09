using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using Serilog;


namespace NewAudio.Nodes
{
    public class AudioGeneratorInitParams : AudioNodeInitParams
    {
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<bool> Interleaved;
        public AudioParam<int> SampleCount;
        public AudioParam<int> Channels;
        public AudioParam<int> DesiredLatency;
    }
    public class AudioGeneratorPlayParams : AudioNodePlayParams
    {
        
    }

    public class AudioGenerator : AudioNode<AudioGeneratorInitParams, AudioGeneratorPlayParams>
    {
        private readonly ILogger _logger;
        private AudioGeneratorBlock _audioGeneratorBlock;
        private AudioFormat _format;
        public WaveFormat WaveFormat => _format.WaveFormat;

        public AudioGenerator()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioGenerator>();
            _logger.Information("AudioGenerator created");
            _format = new AudioFormat(48000, 512, 1);
        }

        public AudioLink Update(SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100, int sampleCount = 512,
            int channels = 2, int desiredLatency = 250, bool interleaved= true)
        {
            InitParams.SamplingFrequency.Value = samplingFrequency;
            InitParams.SampleCount.Value = sampleCount;
            InitParams.DesiredLatency.Value = desiredLatency;
            InitParams.Channels.Value = channels;
            InitParams.Interleaved.Value = interleaved;

            return base.Update();
        }

        public override bool IsInitValid()
        {
            return InitParams.SampleCount.Value > 0 && InitParams.Channels.Value > 0;
        }

        public override Task<bool> Init()
        {
            _format = new AudioFormat((int)InitParams.SamplingFrequency.Value, InitParams.SampleCount.Value, InitParams.Channels.Value, InitParams.Interleaved.Value);
            _audioGeneratorBlock = new AudioGeneratorBlock();
            Output.Format = _format;
            _audioGeneratorBlock.Create(Output.TargetBlock, _format);
            
            return Task.FromResult(true);
        }

        public override async Task<bool> Free()
        {
            await _audioGeneratorBlock.Free();
            return true;
        }

        public override bool Play()
        {
            // Output.Source = _audioGeneratorBlock;
            Output.Play();
            return true;
        }

        public override bool Stop()
        {
            Output.Stop();
            // Output.Source = null;
            return true;
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
    }

}