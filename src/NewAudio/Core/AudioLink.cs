using System;
using System.Threading.Tasks.Dataflow;
using Serilog;

namespace NewAudio.Core
{
    public class AudioLink : IDisposable
    {
        private readonly BroadcastBlock<AudioDataMessage>
            _broadcastBlock = new BroadcastBlock<AudioDataMessage>(i => i);

        private readonly ILogger _logger = Log.ForContext<AudioLink>();
        private IDisposable _currentLink;

        private ISourceBlock<AudioDataMessage> _sourceBlock;

        public AudioFormat Format { get; set; }

        public AudioLink()
        {
            AudioService.Instance.Graph.AddLink(this);
        }

        public ISourceBlock<AudioDataMessage> SourceBlock
        {
            get => _broadcastBlock;
            set
            {
                if (_sourceBlock != null)
                {
                    _currentLink.Dispose();
                }
                // todo only broadcast if necessary
                if (value != null)
                {
                    _sourceBlock = value;
                    _currentLink = _sourceBlock.LinkTo(_broadcastBlock);
                }
            }
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
                    _currentLink?.Dispose();
                    
                }

                _disposedValue = disposing;
            }
        }

    }
}