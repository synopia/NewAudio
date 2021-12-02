using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewAudio.Dispatcher;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;

namespace NewAudio.Processor
{
    
    public class AudioGraph: AudioProcessor, IAsyncUpdater, IChangeBroadcaster
    {
        private static void UpdateOnMessageThread(IAsyncUpdater updater)
        {
            if (Dispatcher.Dispatcher.Instance.IsThisDispatcherThread())
            {
                updater.HandleAsyncUpdate();
            }
            else
            {
                updater.TriggerAsyncUpdate();
            }
        }
        private AsyncUpdateSupport _asyncUpdate;
        private ChangeBroadcaster _changeBroadcaster;
        private List<Node> _nodes = new();
        private NodeId _lastId = new();

        private (int, int, bool) _prepareSettings;
        private bool _isPrepared;

        public List<Node> Nodes=>_nodes;
        public int NodeCount => _nodes.Count;
        public RenderingProgram Program => _program;

        public override string Name => "Audio Graph";


        public AudioGraph()
        {
            _asyncUpdate = new AsyncUpdateSupport(HandleAsyncUpdate);
            _changeBroadcaster = new ChangeBroadcaster();
        }

        public void SendChangeMessage()
        {
            _changeBroadcaster.SendChangeMessage();
        }

        public void TriggerAsyncUpdate()
        {
            _asyncUpdate.TriggerAsyncUpdate();
        }

        public void CancelPendingUpdate()
        {
            _asyncUpdate.CancelPendingUpdate();
        }

        public void HandleUpdateNow()
        {
            _asyncUpdate.HandleUpdateNow();
        }

        public bool IsUpdatePending()
        {
            return _asyncUpdate.IsUpdatePending();
        }

        public void Clear()
        {
            lock (ProcessLock)
            {
                if (_nodes.Count == 0)
                {
                    return;
                }
                _nodes.Clear();
                TopologyChanged();
            }
        }

        public Node GetNode(int index)
        {
            throw new NotImplementedException();
        }

        public Node GetNode(NodeId id)
        {
            foreach (var node in _nodes)
            {
                if (node.NodeId == id)
                {
                    return node;
                }
            }

            return null;
        }

        public Node AddNode(AudioProcessor processor, NodeId nodeId=default)
        {
            if (processor == null)
            {
                // todo error
                return default;
            }

            if (nodeId == default)
            {
                nodeId.Uid = ++_lastId.Uid;
            }
            
            foreach (var node in _nodes)
            {
                if (node.Processor == processor || node.NodeId == nodeId)
                {
                    // todo error
                    return default;
                }
            }

            if (_lastId < nodeId)
            {
                _lastId = nodeId;
            }

            processor.PlayHead = PlayHead;
            Node n = new Node(nodeId, processor);
            lock (ProcessLock)
            {
                _nodes.Add(n);
            }
            
            n.SetParentGraph(this);
            TopologyChanged();
            return n;
        }

        public Node RemoveNode(Node node)
        {
            if (node != null)
            {
                return RemoveNode(node.NodeId);
            }
            Trace.Assert(false);
            return null;
        }
        public Node RemoveNode(NodeId nodeId)
        {
            lock (ProcessLock)
            {
                var index = _nodes.FindIndex(n => n.NodeId == nodeId);
                DisconnectNode(nodeId);
                var node = _nodes[index];
                _nodes.RemoveAt(index);
                TopologyChanged();
                return node;
            }
        }

        public bool IsConnected(NodeId sourceId, NodeId targetId)
        {
            var source = GetNode(sourceId);
            var target = GetNode(targetId);
            if (source != null && target != null)
            {
                foreach (var output in source.Outputs)
                {
                    if (output.OtherNode == target)
                    {
                        return true;
                    }
                }

            }

            return false;
        }

        public bool IsAnInputTo(Node source, Node target)
        {
            Trace.Assert(_nodes.Contains(source));
            Trace.Assert(_nodes.Contains(target));

            return IsAnInputTo(source, target, _nodes.Count);

        }

        public bool CanConnect(Connection connection)
        {
            var source = GetNode(connection.source.NodeId);
            var target = GetNode(connection.target.NodeId);
            if (source != null && target != null)
            {
                return CanConnect(source, connection.source.ChannelIndex, target, connection.target.ChannelIndex);
            }

            return false;
        }

        public List<Connection> GetConnections()
        {
            List<Connection> connections = new List<Connection>();
            foreach (var node in _nodes)
            {
                connections.AddRange(GetNodeConnections(node));
            }
            connections.Sort();
            return connections.Distinct().ToList();
        }

        public bool AddConnection(Connection connection)
        {
            var source = GetNode(connection.source.NodeId);
            var target = GetNode(connection.target.NodeId);
            if (source != null && target!=null)
            {
                var sourceChannel = connection.source.ChannelIndex;
                var targetChannel = connection.target.ChannelIndex;
                if (CanConnect(source, sourceChannel, target, targetChannel))
                {
                    source.Outputs.Add(new Node.Connection(target, targetChannel, sourceChannel));
                    target.Inputs.Add(new Node.Connection(source, sourceChannel, targetChannel));
                    Trace.Assert(IsConnected(connection));
                    TopologyChanged();
                    return true;
                }
            }

            return false;

        }
        public bool RemoveConnection(Connection connection)
        {
            var source = GetNode(connection.source.NodeId);
            var target = GetNode(connection.target.NodeId);
            if (source != null && target != null)
            {
                var sourceChannel = connection.source.ChannelIndex;
                var targetChannel = connection.target.ChannelIndex;
                if (IsConnected(source, sourceChannel, target, targetChannel))
                {
                    source.Outputs.RemoveAll(c => c.Equals(new Node.Connection(target, targetChannel, sourceChannel)));
                    target.Inputs.RemoveAll(c => c.Equals(new Node.Connection(source, sourceChannel, targetChannel)));
                    TopologyChanged();
                    return true;
                }
            }

            return false;
        }

        public bool DisconnectNode(NodeId nodeId)
        {
            var node = GetNode(nodeId);
            if (node != null)
            {
                var connections =  GetNodeConnections(node);
                if (connections.Count > 0)
                {
                    foreach (var connection in connections)
                    {
                        RemoveConnection(connection);
                    }

                    return true;
                }
            }

            return false;
        }

        public bool IsConnectionLegal(Connection connection)
        {
            var source = GetNode(connection.source.NodeId);
            var target = GetNode(connection.source.NodeId);
            if (source != null && target != null)
            {
                return IsLegal(source, connection.source.ChannelIndex, target, connection.target.ChannelIndex);
            }

            return false;
        }

        public bool RemoveIllegalConnection()
        {
            bool anyRemoved = false;
            foreach (var node in _nodes)
            {
                var connections = GetNodeConnections(node);
                foreach (var connection in connections)
                {
                    if (!IsConnectionLegal(connection))
                    {
                        anyRemoved = RemoveConnection(connection) || anyRemoved;
                    }
                }
            }

            return anyRemoved;
        }
        public bool IsConnected(Connection connection)
        {
            var source = GetNode(connection.source.NodeId);
            var target = GetNode(connection.target.NodeId);
            if (source != null && target != null)
            {
                return IsConnected(source, connection.source.ChannelIndex, target, connection.target.ChannelIndex);
            }

            return false;
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
            lock (ProcessLock)
            {
                SetRateAndFrameSize(sampleRate, framesPerBlock);
                var newPrepareSettings = (sampleRate, framesPerBlock, true);
                if (_prepareSettings != newPrepareSettings)
                {
                    Unprepare();
                    _prepareSettings = newPrepareSettings;
                }
            }
            ClearRenderingSequence();
            UpdateOnMessageThread(this);
        }

        public override void ReleaseResources()
        {
            lock (ProcessLock)
            {
                CancelPendingUpdate();
                Unprepare();
                if (_program != null)
                {
                    _program.ReleaseBuffers();
                }

            }
        }

        public override void Process(AudioBuffer buffer)
        {
            if (!_isPrepared /*&& TODO */)
            {
                HandleAsyncUpdate();
            }
            
            lock (ProcessLock)
            {
                if (_isPrepared)
                {
                    _program?.Perform(buffer, PlayHead);
                }
                else
                {
                    buffer.Zero();
                }
            }
        }

        public override void ProcessBypassed(AudioBuffer buffer)
        {
            base.ProcessBypassed(buffer);
        }

        public override void Reset()
        {
            lock (ProcessLock)
            {
                foreach (var node in _nodes)
                {
                    node.Processor.Reset();
                }
            }
        }

        private void TopologyChanged()
        {
            SendChangeMessage();
            if (_isPrepared)
            {
                UpdateOnMessageThread(this);
            }
        }

        private void Unprepare()
        {
            _prepareSettings.Item3 = false;
            _isPrepared = false;
            foreach (var node in _nodes)
            {
                node.Unprepare();
            }
        }

        public void HandleAsyncUpdate()
        {
            BuildRenderingSequence();
        }

        private void ClearRenderingSequence()
        {
            lock (ProcessLock)
            {
                // todo
            }
        }

        private void BuildRenderingSequence()
        {
            
            var program = new RenderingProgram();
            var b = new RenderingBuilder(this, _program);
            lock (ProcessLock)
            {
                var currentFramesPerBlock = FramesPerBlock;
                _program.PrepareBuffers(currentFramesPerBlock);
                
                if (AnyNodesNeedPreparing())
                {
                    _program.Reset();

                    foreach (var node in _nodes)
                    {
                        node.Prepare(SampleRate, currentFramesPerBlock, this);
                    }
                }

                _isPrepared = true;
                _program = program;
            }
        }

        private bool AnyNodesNeedPreparing()
        {
            return _nodes.Any(n => !n.IsPrepared);

        }

        private bool IsLegal(Node source, int sourceChannel, Node target, int targetChannel)
        {
            return sourceChannel >= 0 && sourceChannel < source.Processor.TotalNumberOfOutputChannels
                                      && targetChannel >= 0 &&
                                      targetChannel < target.Processor.TotalNumberOfInputChannels;

        }
        
        private bool CanConnect(Node source, int sourceChannel, Node target, int targetChannel)
        {
            if (sourceChannel < 0 || targetChannel < 0 || source == target)
            {
                return false;
            }

            if (source == null || sourceChannel >= source.Processor.TotalNumberOfOutputChannels)
            {
                return false;
            }

            if (target == null || targetChannel >= target.Processor.TotalNumberOfInputChannels)
            {
                return false;
            }
            
            return !IsConnected(source, sourceChannel, target, targetChannel);

        }
        private bool IsConnected(Node source, int sourceChannel, Node target, int targetChannel)
        {
            foreach (var output in source.Outputs)
            {
                if (output.OtherNode == target && output.OwnChannel == sourceChannel &&
                    output.OtherChannel == targetChannel)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAnInputTo(Node source, Node target, int recursion)
        {
            foreach (var input in target.Inputs)
            {
                if (input.OtherNode == source)
                {
                    return true;
                }
            }

            if (recursion > 0)
            {
                foreach (var input in target.Inputs)
                {
                    if (IsAnInputTo(source, input.OtherNode, recursion - 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<Connection> GetNodeConnections(Node node)
        {
            var result = new List<Connection>();
            foreach (var input in node.Inputs)
            {
                result.Add(new Connection(new NodeAndChannel(input.OtherNode.NodeId, input.OtherChannel), 
                    new NodeAndChannel(node.NodeId, input.OwnChannel)));
            }
            foreach (var output in node.Outputs)
            {
                result.Add(new Connection(new NodeAndChannel(node.NodeId, output.OwnChannel), 
                    new NodeAndChannel(output.OtherNode.NodeId, output.OtherChannel)));
            }

            return result;
        }
        
        

        public struct NodeId : IEquatable<NodeId>, IComparable<NodeId>
        {
            public int Uid;

            public NodeId(int uid=0)
            {
                Uid = uid;
            }

            public override string ToString()
            {
                return Uid.ToString();
            }

            public bool Equals(NodeId other)
            {
                return Uid == other.Uid;
            }

            public override bool Equals(object obj)
            {
                return obj is NodeId other && Equals(other);
            }

            public override int GetHashCode()
            {
                return Uid;
            }

            public static bool operator ==(NodeId left, NodeId right)
            {
                return left.Equals(right);
            }

            public int CompareTo(NodeId other)
            {
                return Uid.CompareTo(other.Uid);
            }

            public static bool operator <(NodeId left, NodeId right)
            {
                return left.Uid < right.Uid;
            }
            public static bool operator >(NodeId left, NodeId right)
            {
                return left.Uid > right.Uid;
            }
            public static bool operator !=(NodeId left, NodeId right)
            {
                return !left.Equals(right);
            }
        }

        public readonly struct NodeAndChannel : IEquatable<NodeAndChannel>
        {
            public readonly NodeId NodeId;
            public readonly int ChannelIndex;

            public NodeAndChannel(NodeId nodeId, int channelIndex)
            {
                NodeId = nodeId;
                ChannelIndex = channelIndex;
            }

            public override string ToString()
            {
                return $"{NodeId}:{ChannelIndex}";
            }

            public bool Equals(NodeAndChannel other)
            {
                return NodeId.Equals(other.NodeId) && ChannelIndex == other.ChannelIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is NodeAndChannel other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (NodeId.GetHashCode() * 397) ^ ChannelIndex;
                }
            }

            public static bool operator ==(NodeAndChannel left, NodeAndChannel right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(NodeAndChannel left, NodeAndChannel right)
            {
                return !left.Equals(right);
            }
        }

        public class Node
        {
            public NodeId NodeId;
            public AudioProcessor Processor { get; }
            public List<Connection> Inputs = new ();
            public List<Connection> Outputs= new ();
            public bool IsPrepared { get; private set; }
            private bool _bypassed;

            public bool IsBypassed
            {
                get
                {
                    /*
                    if (Processor != null)
                    {
                        var p = Processor.BypassParameter;
                        if (p != null)
                        {
                            return p.Value != 0.0f;
                        }
                    }
                    */

                    return _bypassed;
                }
                set
                {
                    /*
                    if (Processor != null)
                    {
                        var p = Processor.BypassParameter;
                        if (p != null)
                        {
                            p.Value = value ? 1.0f : 0.0f;
                        }
                    }
                    */

                    _bypassed = value;
                }
            }

            private object _processorLock = new object();

            public readonly struct Connection : IEquatable<Connection>
            {
                public readonly Node OtherNode;
                public readonly int OtherChannel;
                public readonly int OwnChannel;

                public Connection(Node otherNode, int otherChannel, int ownChannel)
                {
                    OtherNode = otherNode;
                    OtherChannel = otherChannel;
                    OwnChannel = ownChannel;
                }

                public bool Equals(Connection other)
                {
                    return Equals(OtherNode, other.OtherNode) && OtherChannel == other.OtherChannel && OwnChannel == other.OwnChannel;
                }

                public override bool Equals(object obj)
                {
                    return obj is Connection other && Equals(other);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        var hashCode = (OtherNode != null ? OtherNode.GetHashCode() : 0);
                        hashCode = (hashCode * 397) ^ OtherChannel;
                        hashCode = (hashCode * 397) ^ OwnChannel;
                        return hashCode;
                    }
                }

                public static bool operator ==(Connection left, Connection right)
                {
                    return left.Equals(right);
                }

                public static bool operator !=(Connection left, Connection right)
                {
                    return !left.Equals(right);
                }
            }

            public Node(NodeId nodeId, AudioProcessor processor)
            {
                NodeId = nodeId;
                Processor = processor;
            }

            public void SetParentGraph(AudioGraph graph)
            {
                lock (_processorLock)
                {
                    if (Processor is AudioGraphIOProcessor ioProcessor)
                    {
                        ioProcessor.ParentGraph = graph;
                    }
                }
            }

            public void Prepare(int newSampleRate, int newFramesPerBlock, AudioGraph graph)
            {
                lock (_processorLock)
                {
                    if (!IsPrepared)
                    {
                        SetParentGraph(graph);
                        Processor.SetRateAndFrameSize(newSampleRate, newFramesPerBlock);
                        Processor.PrepareToPlay(newSampleRate, newFramesPerBlock);
                        IsPrepared = true;
                    }
                }
            }

            public void Unprepare()
            {
                lock (_processorLock)
                {
                    if (IsPrepared)
                    {
                        IsPrepared = false;
                        Processor.ReleaseResources();
                    }
                }
            }

            public void Process(AudioBuffer buffer)
            {
                lock (_processorLock)
                {
                    Processor.Process(buffer);
                }                
            }
            public void ProcessBypassed(AudioBuffer buffer)
            {
                lock (_processorLock)
                {
                    Processor.ProcessBypassed(buffer);
                }
            }
        }

        public readonly struct Connection : IEquatable<Connection>, IComparable<Connection>
        {
            public readonly NodeAndChannel source;
            public readonly NodeAndChannel target;

            public Connection(NodeAndChannel source, NodeAndChannel target)
            {
                this.source = source;
                this.target = target;
            }

            public override string ToString()
            {
                return $"{source} => {target}";
            }

            public bool Equals(Connection other)
            {
                return source.Equals(other.source) && target.Equals(other.target);
            }

            public override bool Equals(object obj)
            {
                return obj is Connection other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (source.GetHashCode() * 397) ^ target.GetHashCode();
                }
            }

            public static bool operator ==(Connection left, Connection right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Connection left, Connection right)
            {
                return !left.Equals(right);
            }
            public int CompareTo(Connection other)
            {
                if (source.NodeId != other.source.NodeId)
                {
                    return source.NodeId.CompareTo(other.source.NodeId);
                }
                if (target.NodeId != other.target.NodeId)
                {
                    return target.NodeId.CompareTo(other.target.NodeId);
                }
                if (source.ChannelIndex != other.source.ChannelIndex)
                {
                    return source.ChannelIndex.CompareTo(other.source.ChannelIndex);
                }
                return target.ChannelIndex.CompareTo(other.target.ChannelIndex);
            }

            public static bool operator >(Connection left, Connection right)
            {
                return left != right && !(left < right);
            }
            public static bool operator <(Connection left, Connection right)
            {
                if (left.source.NodeId != right.source.NodeId)
                {
                    return left.source.NodeId < right.source.NodeId;
                }
                if (left.target.NodeId != right.target.NodeId)
                {
                    return left.target.NodeId < right.target.NodeId;
                }
                if (left.source.ChannelIndex != right.source.ChannelIndex)
                {
                    return left.source.ChannelIndex < right.source.ChannelIndex;
                }
                return left.target.ChannelIndex < right.target.ChannelIndex;
            }
        }
        
        
        private bool _disposedValue;
        private RenderingProgram _program;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    CancelPendingUpdate();
                    ClearRenderingSequence();
                    Clear();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}