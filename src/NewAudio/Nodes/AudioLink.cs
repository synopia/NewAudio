using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewAudio.Processor;
using VL.Lib.Basics.Resources;
using NewAudio;
using NewAudio.Nodes;

namespace NewAudio.Core
{
    
    public class AudioLink : IDisposable
    {
        public AudioGraph.Node Node { get; }
        private int[] _channels = {0};

        public AudioLink(AudioGraph.Node node)
        {
            Node = node;
        }

        public IEnumerable<AudioGraph.NodeAndChannel> NodeAndChannels
        {
            get { return _channels.Select(ch => new AudioGraph.NodeAndChannel(Node.NodeId, ch)); }
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
                }

                _disposedValue = disposing;
            }
        }
    }
}