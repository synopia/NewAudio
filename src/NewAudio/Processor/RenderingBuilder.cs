using System;
using System.Collections.Generic;
using System.Diagnostics;
using VL.Model;

namespace NewAudio.Processor
{
    using Node = AudioGraph.Node;
    using NodeId = AudioGraph.NodeId;
    
    public class AssignedBuffer
    {
        public AudioGraph.NodeAndChannel Channel;

        public static AssignedBuffer CreateReadOnlyEmpty()
        {
            return new AssignedBuffer() { Channel = new AudioGraph.NodeAndChannel(ZeroNodeId, 0) };
        }
        public static AssignedBuffer CreateFree()
        {
            return new AssignedBuffer() { Channel = new AudioGraph.NodeAndChannel(FreeNodeId, 0) };
        }

        public bool IsReadOnlyEmpty => Channel.NodeId == ZeroNodeId;
        public bool IsFree => Channel.NodeId == FreeNodeId;
        public bool IsAssigned => !IsReadOnlyEmpty && !IsFree;

        public void SetFree()
        {
            Channel = new AudioGraph.NodeAndChannel(FreeNodeId, 0);
        }

        public void SetAssignedToAnon()
        {
            Channel = new AudioGraph.NodeAndChannel(AnonNodeId, 0);
        }

        private static readonly NodeId AnonNodeId = new(-1);
        private static readonly NodeId ZeroNodeId = new(-2);
        private static readonly NodeId FreeNodeId = new(-2);
    }
    
    public class RenderingBuilder
    {
        
        private AudioGraph _graph;
        private RenderingProgram _program;
        private List<Node> _orderedNodes = new();
        private List<AssignedBuffer> _audioBuffers = new ();
        private Dictionary<int, int> _delays = new ();
        private int _totalLatency;

        public RenderingBuilder(AudioGraph graph, RenderingProgram program)
        {
            _graph = graph;
            _program = program;
            _orderedNodes = CreateOrderedNodeList(graph);
            _audioBuffers.Add(AssignedBuffer.CreateReadOnlyEmpty());
            
            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                CreateRenderingOpsForNode(_orderedNodes[i], i);
                MarkAnyUnusedBuffersFree(_audioBuffers, i);
            }

            graph.LatencySamples = _totalLatency;
            _program.NumBuffersNeeded = _audioBuffers.Count;
        }

        private int GetNodeDelay(NodeId nodeId)
        {
            return _delays[nodeId.Uid];
        }

        private int GetInputLatencyForNode(NodeId nodeId)
        {
            int maxLatency = 0;
            foreach (var connection in _graph.GetConnections())
            {
                if (connection.target.NodeId == nodeId)
                {
                    maxLatency = Math.Max(maxLatency, GetNodeDelay(connection.source.NodeId));
                }
            }

            return maxLatency;
        }

        public static void GetAllParentsOfNode(Node child, HashSet<Node> parents, Dictionary<Node, HashSet<Node>> otherParents)
        {
            foreach (var input in child.Inputs)
            {
                var parentNode = input.OtherNode;
                if (parentNode == child)
                {
                    continue;
                }

                if (parents.Add(parentNode))
                {
                    if (otherParents.TryGetValue(parentNode, out HashSet<Node> parentParents))
                    {
                        parents.AddRange(parentParents);
                        continue;
                    }
                    GetAllParentsOfNode(input.OtherNode, parents, otherParents);
                }
            }
        }

        public static List<Node> CreateOrderedNodeList(AudioGraph graph)
        {
            List<Node> result = new();
            Dictionary<Node, HashSet<Node>> nodeParents = new();
            foreach (var node in graph.Nodes)
            {
                int insertionIndex = 0;
                for (; insertionIndex < result.Count; insertionIndex++)
                {
                    var parents = nodeParents[result[insertionIndex]];
                    if (parents.Contains(node))
                    {
                        break;
                    }
                }
                
                result.Insert(insertionIndex, node);
                if (!nodeParents.ContainsKey(node))
                {
                    nodeParents[node] = new HashSet<Node>();
                }
                GetAllParentsOfNode(node, nodeParents[node], nodeParents);
            }

            return result;
        }


        public int FindBufferForInput(Node node, int inputChannel, int renderingIndex, int maxLatency)
        {
            var processor = node.Processor;
            var numOuts = processor.TotalNumberOfOutputChannels;
            var sources = GetSourcesForChannel(node, inputChannel);
            if (sources.Count == 0)
            {
                if (inputChannel >= numOuts)
                {
                    return 0;
                }

                var index = GetFreeBuffer(_audioBuffers);
                _program.AddClearChannelOp(index);
                return index;
            }

            int bufIndex;
            if (sources.Count == 1)
            {
                var src = sources[0];
                bufIndex = GetBufferContaining(src);
                if (bufIndex < 0)
                {
                    bufIndex = 0;
                }

                if (inputChannel < numOuts && IsBufferNeededLater(renderingIndex, inputChannel, src))
                {
                    var newFreeBuffer = GetFreeBuffer(_audioBuffers);
                    _program.AddCopyChannelOp(bufIndex, newFreeBuffer);
                    bufIndex = newFreeBuffer;
                }

                var nodeDelay = GetNodeDelay(src.NodeId);
                if (nodeDelay < maxLatency)
                {
                    _program.AddDelayChannelOp(bufIndex, maxLatency-nodeDelay);
                }

                return bufIndex;
            }

            int reusableInputIndex = -1;
            bufIndex = -1;

            for (var i = 0; i < sources.Count; i++)
            {
                var src = sources[i];
                var sourceBufIndex = GetBufferContaining(src);
                if (sourceBufIndex >= 0 && !IsBufferNeededLater(renderingIndex, inputChannel, src))
                {
                    reusableInputIndex = i;
                    bufIndex = sourceBufIndex;
                    var nodeDelay = GetNodeDelay(src.NodeId);
                    if (nodeDelay < maxLatency)
                    {
                        _program.AddDelayChannelOp(bufIndex, maxLatency-nodeDelay);
                    }                    
                    break;
                }
            }

            if (reusableInputIndex < 0)
            {
                bufIndex = GetFreeBuffer(_audioBuffers);
                Trace.Assert(bufIndex!=0);
                _audioBuffers[bufIndex].SetAssignedToAnon();
                var srcIndex = GetBufferContaining(sources[0]);
                if (srcIndex < 0)
                {
                    _program.AddClearChannelOp(bufIndex);
                }
                else
                {
                    _program.AddCopyChannelOp(srcIndex, bufIndex);
                }

                reusableInputIndex = 0;
                var nodeDelay = GetNodeDelay(sources[0].NodeId);
                if (nodeDelay < maxLatency)
                {
                    _program.AddDelayChannelOp(bufIndex, maxLatency-nodeDelay);
                }                   
                
            }

            for (int i = 0; i < sources.Count; i++)
            {
                if (i != reusableInputIndex)
                {
                    var src = sources[i];
                    var srcIndex = GetBufferContaining(src);
                    if (srcIndex >= 0)
                    {
                        var nodeDelay = GetNodeDelay(src.NodeId);
                        if (nodeDelay < maxLatency)
                        {
                            if (!IsBufferNeededLater(renderingIndex, inputChannel, src))
                            {
                                _program.AddDelayChannelOp(bufIndex, maxLatency-nodeDelay);
                            }
                            else
                            {
                                var bufferToDelay = GetFreeBuffer(_audioBuffers);
                                _program.AddCopyChannelOp(srcIndex, bufferToDelay);
                                _program.AddDelayChannelOp(bufferToDelay, maxLatency-nodeDelay);
                                srcIndex = bufferToDelay;
                            }
                        }
                        _program.AddAddChannelOp(srcIndex, bufIndex);
                    }
                }
            }

            return bufIndex;
        }

        private void CreateRenderingOpsForNode(Node node, int renderingIndex)
        {
            var processor = node.Processor;
            var numIns = processor.TotalNumberOfInputChannels;
            var numOuts = processor.TotalNumberOfOutputChannels;
            var totalChannels = Math.Max(numIns, numOuts);
            var channelsToUse = new List<int>();
            var maxLatency = GetInputLatencyForNode(node.NodeId);
            for (int inputChannel = 0; inputChannel < numIns; inputChannel++)
            {
                var index = FindBufferForInput(node, inputChannel, renderingIndex, maxLatency);
                Trace.Assert(index>=0);
                channelsToUse.Add(index);
                if (inputChannel < numOuts)
                {
                    var b = _audioBuffers[index];
                    b.Channel = new AudioGraph.NodeAndChannel(node.NodeId, inputChannel);
                }
            }

            for (int outputChannel = numIns; outputChannel < numOuts; outputChannel++)
            {
                var index = GetFreeBuffer(_audioBuffers);
                Trace.Assert(index!=0);
                channelsToUse.Add(index );
                var b = _audioBuffers[index];
                b.Channel = new AudioGraph.NodeAndChannel(node.NodeId, outputChannel);
            }

            _delays[node.NodeId.Uid] = maxLatency + processor.LatencySamples;
            if (numOuts == 0)
            {
                _totalLatency = maxLatency;
            }
            _program.AddProcessOp(node,channelsToUse, totalChannels);
        }

        private List<AudioGraph.NodeAndChannel> GetSourcesForChannel(Node node, int inputChannelIndex)
        {
            var result = new List<AudioGraph.NodeAndChannel>();
            AudioGraph.NodeAndChannel nc = new AudioGraph.NodeAndChannel(node.NodeId, inputChannelIndex);
            foreach (var connection in _graph.GetConnections())
            {
                if (connection.target == nc)
                {
                    result.Add(connection.source);
                }
            }

            
            return result;
        }

        private static int GetFreeBuffer(List<AssignedBuffer> buffers)
        {
            for (int i = 1; i < buffers.Count; i++)
            {
                if (buffers[i].IsFree)
                {
                    return i;
                }
            }
            
            buffers.Add(AssignedBuffer.CreateFree());
            return buffers.Count - 1;
        }

        private int GetBufferContaining(AudioGraph.NodeAndChannel output)
        {
            int i = 0;
            foreach (var buffer in _audioBuffers)
            {
                if (buffer.Channel == output)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        private  void MarkAnyUnusedBuffersFree(List<AssignedBuffer> buffers, int stepIndex)
        {
            foreach (var buffer in buffers)
            {
                if (buffer.IsAssigned && !IsBufferNeededLater(stepIndex, -1, buffer.Channel))
                {
                    buffer.SetFree();
                }
            }
        }

        private bool IsBufferNeededLater(int stepIndex, int inputChannel, AudioGraph.NodeAndChannel output)
        {
            while (stepIndex<_orderedNodes.Count)
            {
                var node = _orderedNodes[stepIndex];
                for (int i = 0; i < node.Processor.TotalNumberOfInputChannels; i++)
                {
                    if (i != inputChannel &&
                        _graph.IsConnected(new AudioGraph.Connection(output,
                            new AudioGraph.NodeAndChannel(node.NodeId, i))))
                    {
                        return true;
                    }
                }

                inputChannel = -1;
                ++stepIndex;
            }

            return false;
        }
    }
}