using System;
using System.Threading.Tasks.Dataflow;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Core
{
    public class AudioLink : IDisposable
    {
        private readonly IResourceHandle<AudioGraph> _graph;

        public readonly BroadcastBlock<AudioDataMessage> TargetBlock = new(i => i);
        private ISourceBlock<AudioDataMessage> _currentSourceBlock;
        private IDisposable _currentSourceLink;

        public AudioFormat Format { get; set; }

        public AudioLink() 
        {
            _graph = Factory.GetAudioGraph();
            _graph.Resource.AddLink(this);
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

        public void Dispose()
        {
            Dispose(true);
        }

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
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