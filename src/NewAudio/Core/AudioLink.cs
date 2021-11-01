using System;
using System.Threading.Tasks.Dataflow;
using Serilog;

namespace NewAudio.Core
{
    public class AudioLink : IDisposable {        
        
        private readonly ILogger _logger = Log.ForContext<AudioLink>();
        
        private ISourceBlock<IAudioMessage> _sourceBlock;
        private readonly BroadcastBlock<IAudioMessage> _broadcastBlock = new BroadcastBlock<IAudioMessage>(i=> i);
        private IDisposable _currentLink;

        public  Action<ISourceBlock<IAudioMessage>> Connect;
        public  Action<ISourceBlock<IAudioMessage>> Disconnect;
        public  Action<ISourceBlock<IAudioMessage>, ISourceBlock<IAudioMessage>> Reconnect;

        public ISourceBlock<IAudioMessage> SourceBlock
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

        public AudioLink()
        {
            AudioService.Instance.Graph.Add(this);
        }

        public void Dispose()
        {
            _currentLink?.Dispose();
            if (_sourceBlock is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            AudioService.Instance.Graph.Remove(this);
        }
    }
}