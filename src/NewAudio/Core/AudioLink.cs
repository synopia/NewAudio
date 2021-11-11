using System;
using System.Threading.Tasks.Dataflow;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Core
{
    public class AudioLink : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IResourceHandle<AudioGraph> _graph;
        
        public readonly BroadcastBlock<AudioDataMessage> TargetBlock= new(i=>i);
        private ISourceBlock<AudioDataMessage> _currentSourceBlock;
        private IDisposable _currentSourceLink;

        public AudioFormat Format { get; set; }

        public AudioLink(): this(VLApi.Instance){}
        public AudioLink(IVLApi api)
        {
            _graph = api.GetAudioGraph();
            _graph.Resource.AddLink(this);
            _logger = _graph.Resource.GetLogger<AudioLink>();
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
            _logger.Information("Dispose called for AudioLink {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _graph.Resource.RemoveLink(this);
                    _currentSourceLink?.Dispose();
                    _currentSourceLink = null;
                }

                _disposedValue = disposing;
            }
        }

   
    }
}