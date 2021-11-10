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
        public readonly BroadcastBlock<AudioDataMessage> TargetBlock= new(i=>i);
        private ISourceBlock<AudioDataMessage> _currentSourceBlock;
        private IDisposable _currentSourceLink;

        public AudioFormat Format { get; set; }

        public AudioLink()
        {
            AudioService.Instance.Graph.AddLink(this);
        }

        public ISourceBlock<AudioDataMessage> SourceBlock
        {
            get => TargetBlock;
            set
            {
                if (_currentSourceBlock != null)
                {
                    _currentSourceLink?.Dispose();
                    _currentSourceLink = null;
                }

                // todo only broadcast if necessary
                if (value != null)
                {
                    _currentSourceBlock = value;
                    _currentSourceLink = _currentSourceBlock.LinkTo(TargetBlock);
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
                    _currentSourceLink?.Dispose();
                    _currentSourceLink = null;
                }

                _disposedValue = disposing;
            }
        }

   
    }
}