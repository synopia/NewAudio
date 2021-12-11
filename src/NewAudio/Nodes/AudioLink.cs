using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VL.NewAudio.Processor;
using VL.Lib.Basics.Resources;
using VL.NewAudio;
using VL.NewAudio.Nodes;

namespace VL.NewAudio.Nodes
{
    public class AudioLink : IDisposable
    {
        public AudioGraph.Node Node { get; private set; }
        private AudioGraph.NodeAndChannel[] _channels;

        public void Create(AudioGraph.Node node)
        {
            Node = node;
            _channels = new AudioGraph.NodeAndChannel[node.Processor.TotalNumberOfOutputChannels];
            for (var i = 0; i < _channels.Length; i++)
            {
                _channels[i] = new AudioGraph.NodeAndChannel(node.NodeId, i);
            }
        }

        public IEnumerable<AudioGraph.NodeAndChannel> NodeAndChannels => _channels;

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