using System.Threading.Tasks;
using NewAudio.Blocks;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Nodes
{
    public class AudioJoinerInitParams : AudioNodeInitParams
    {
    }

    public class AudioJoinerPlayParams : AudioNodePlayParams
    {
        public AudioParam<AudioLink> Input2;
    }

    public class AudioJoiner : AudioNode<AudioJoinerInitParams, AudioJoinerPlayParams>
    {
        private readonly ILogger _logger;

        private JoinAudioBlock _joinAudioBlock;
        // private AudioFormat _format;
        // public WaveFormat WaveFormat => _format.WaveFormat;


        public AudioJoiner()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioJoiner>();
            _logger.Information("AudioJoiner device created");
        }

        public AudioLink Update(AudioLink input, AudioLink input2)
        {
            PlayParams.Input.Value = input;
            PlayParams.Input2.Value = input2;

            return base.Update();
        }

        public override bool IsPlayValid()
        {
            // todo only one link should work too
            return PlayParams.Input.Value != null && PlayParams.Input2.Value != null &&
                   PlayParams.Input.Value.SourceBlock != null && PlayParams.Input2.Value.SourceBlock != null;
        }

        public override bool Play()
        {
            _joinAudioBlock = new JoinAudioBlock();
            _joinAudioBlock.Create(PlayParams.Input.Value, PlayParams.Input2.Value);
            Output.SourceBlock = _joinAudioBlock;
            Output.Format = _joinAudioBlock.OutputFormat;
            return true;
        }

        public override bool Stop()
        {
            _joinAudioBlock?.Dispose();
            return true;
        }

        public override Task<bool> Init()
        {
            return Task.FromResult(true);
        }

        public override Task<bool> Free()
        {
            return Task.FromResult(true);
        }

        public override string DebugInfo()
        {
            return $"JOIN: [ out={_joinAudioBlock?.OutputBufferCount} {base.DebugInfo()} ]";
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _joinAudioBlock?.Dispose();
                    _joinAudioBlock = null;
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}