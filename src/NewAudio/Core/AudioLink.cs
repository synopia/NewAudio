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

        public Action<ISourceBlock<AudioDataMessage>> Connect;
        public Action<ISourceBlock<AudioDataMessage>> Disconnect;
        public Action<ISourceBlock<AudioDataMessage>, ISourceBlock<AudioDataMessage>> Reconnect;

        public AudioFormat Format { get; set; }
        public AudioLink()
        {
            AudioService.Instance.Graph.Add(this);
        }

        public ISourceBlock<AudioDataMessage> SourceBlock
        {
            get => _broadcastBlock;
            // get => _sourceBlock;
            set
            {
                if (_sourceBlock == null)
                {
                    if (value != null)
                    {
                        _sourceBlock = value;
                        _currentLink = _sourceBlock.LinkTo(_broadcastBlock);
                        Connect?.Invoke(value);
                    }
                }
                else
                {
                    if (value == null)
                    {
                        Disconnect?.Invoke(_sourceBlock);
                        _sourceBlock = null;
                    }
                    else
                    {
                        _sourceBlock.Complete();
                        _sourceBlock.Completion.ContinueWith(task =>
                        {
                            // todo
                            var old = _sourceBlock;
                            _currentLink?.Dispose();
                            _sourceBlock = value;
                            _currentLink = _sourceBlock?.LinkTo(_broadcastBlock);
                            Reconnect?.Invoke(old, value);
                        });
                    }
                }
            }
        }

        public void Dispose()
        {
            _currentLink?.Dispose();
            if (_sourceBlock is IDisposable disposable) disposable.Dispose();

            AudioService.Instance.Graph.Remove(this);
        }
    }
}