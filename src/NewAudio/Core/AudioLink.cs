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
                // todo only broadcast if necessary
                if (_sourceBlock == null)
                {
                    if (value != null)
                    {
                        _sourceBlock = value;
                        _currentLink = _sourceBlock.LinkTo(_broadcastBlock);
                        return;
                    }
                }

                throw new Exception("Resetting SourceBlock is not supported!");
            }
        }

        public void Dispose()
        {
            _logger.Information("Dispose called!");
            _currentLink?.Dispose();
            AudioService.Instance.Graph.RemoveLink(this);
        }
    }
}