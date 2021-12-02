using System;
using System.Diagnostics;
using NewAudio.Processor;
using VL.Lib.Basics.Resources;
using NewAudio;
using NewAudio.Nodes;

namespace NewAudio.Core
{
    
    public class AudioLink : IDisposable
    {
        private IAudioProcessorNode _owner;
        private AudioGraph.Connection? _connection;
        private AudioGraph? _graph;
        
        public AudioFormat Format { get; set; }

        public AudioLink(IAudioProcessorNode owner)
        {
            _owner = owner;
            
        }

        public void Disconnect()
        {
            Trace.Assert(_graph!=null);
            
            if (_connection.HasValue)
            {
                _graph!.RemoveConnection(_connection.Value);
            }

            _owner.RemoveFromGraph();
            _graph = null;
            _connection = null;
        }
        public AudioGraph.Node Connect(AudioGraph graph, AudioGraph.Node? target=null)
        {
            _graph = graph;
            var source = _owner.AddToGraph(graph);
            if (target != null)
            {
                _connection = new AudioGraph.Connection(new AudioGraph.NodeAndChannel(source.NodeId, 0),
                    new AudioGraph.NodeAndChannel(target.NodeId, 0));
                _graph?.AddConnection(_connection.Value);
            }
            
            return source;
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