using System.Linq;
using VL.NewAudio.Core;
using VL.NewAudio.Processor;
using VL.Lang;

namespace VL.NewAudio.Nodes
{
    public class AudioProcessorNode<TProcessor> : AudioNode where TProcessor : AudioProcessor
    {
        public readonly TProcessor Processor;

        private AudioGraph.Node? _node;
        private AudioGraph _graph;
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

                if (_input != null)
                {
                    var sourceId = _input.Node.NodeId;
                    var targetId = _node.NodeId;
                    foreach (var connection in _graph.GetConnections()
                                 .Where(c => c.source.NodeId == sourceId && c.target.NodeId == targetId))
                    {
                        _graph.RemoveConnection(connection);
                    }
                }

                _input = value;

                if (_input != null)
                {
                    var ch = 0;
                    foreach (var input in _input.NodeAndChannels)
                    {
                        var connection =
                            new AudioGraph.Connection(input, new AudioGraph.NodeAndChannel(_node.NodeId, ch));
                        _graph.AddConnection(connection);
                        ch++;
                        if (ch >= Processor.TotalNumberOfInputChannels)
                        {
                            ch = 0;
                        }
                    }
                }
            }
        }

        public AudioProcessorNode(TProcessor processor)
        {
            Processor = processor;
            _graph = AudioGraph.CurrentGraph;
            Output = new AudioLink();
            _node = _graph.AddNode(Processor)!;
            Output.Create(_node);
        }

        public override bool IsEnable
        {
            get => !(_node?.IsBypassed ?? true);
            set => _node!.IsBypassed = !value;
        }

        public override bool IsEnabled => IsEnable;


        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_node != null)
                    {
                        _graph.RemoveNode(_node);
                    }

                    Processor.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}