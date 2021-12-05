using System;
using System.Diagnostics;
using NewAudio.Core;
using NewAudio.Processor;
using VL.Lang;

namespace NewAudio.Nodes
{
    public interface IAudioProcessorNode
    {
        bool RemoveFromGraph();
        AudioGraph.Node AddToGraph(AudioGraph graph);
    }
    public class AudioProcessorNode<TProcessor> : AudioNode, IAudioProcessorNode where TProcessor: AudioProcessor
    {
        public readonly TProcessor Processor;

        private AudioGraph.Node? _node;
        private AudioGraph? _graph;
        private AudioLink? _input;
        
        public readonly AudioLink Output;
        public AudioLink? Input
        {
            get => _input;
            set
            {
                if (_input == value)
                {
                    return;
                }

                _input?.Disconnect();

                _input = value;

                if (_input != null && _graph!=null && _node!=null)
                {
                     _input.Connect(_graph, _node);
                }
            }
        }

        public AudioProcessorNode(TProcessor processor)
        {
            Processor = processor;
            Output = new(this);
        }

        public override Message? Update(ulong mask)
        {
            return null;
        }

        public override bool IsEnabled => true;

        public bool RemoveFromGraph()
        {
            Trace.Assert(_graph!=null && _node!=null);
            _input?.Disconnect();
            _graph!.RemoveNode(_node!);
            _graph = null;
            _node = null;
            return true;
        }
        
        public AudioGraph.Node AddToGraph(AudioGraph graph)
        {
            _graph = graph;
            _node = _graph.AddNode(Processor);
            if (_node == null)
            {
                throw new InvalidOperationException("Cannot create node");
            }
            _input?.Connect(graph, _node);
            return _node;
        }
        
        private bool _disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Processor.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}