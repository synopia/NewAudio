using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using Serilog;

namespace NewAudio.Core
{
    public class AudioLink : IDisposable
    {
        private readonly ILogger _logger = Log.ForContext<AudioLink>();
        private readonly BroadcastBlock<AudioDataMessage> _broadcastBlock= new(i=>i);
        public readonly DynamicBufferBlock TargetBlock = new();
        private ISourceBlock<AudioDataMessage> _currentSourceBlock;
        private IDisposable _currentSourceLink;
        private IDisposable _targetLink;

        public AudioFormat Format { get; set; }

        public AudioLink()
        {
            AudioService.Instance.Graph.AddLink(this);
            _targetLink = TargetBlock.LinkTo(_broadcastBlock);
        }

        public ISourceBlock<AudioDataMessage> SourceBlock
        {
            get => _broadcastBlock;
            set
            {
                if (_currentSourceBlock != null)
                {
                    _currentSourceLink?.Dispose();
                }

                // todo only broadcast if necessary
                if (value != null)
                {
                    _currentSourceBlock = value;
                    _currentSourceLink = _currentSourceBlock.LinkTo(TargetBlock);
                }
            }
        }

        public void Play()
        {
            _targetLink = TargetBlock.LinkTo(_broadcastBlock);
        }

        public void Stop()
        {
            _targetLink?.Dispose();
            _targetLink = null;
        }

        public void Dispose() => Dispose(true);
        
        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            AudioService.Instance.Logger.Information("Dispose called for AudioLink {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    AudioService.Instance.Graph.RemoveLink(this);
                    _targetLink.Dispose();
                    _currentSourceLink?.Dispose();
                    TargetBlock.Dispose();

                    _currentSourceLink = null;
                }

                _disposedValue = disposing;
            }
        }

   
    }
}