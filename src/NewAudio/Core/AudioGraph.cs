using System;
using System.Collections.Generic;
using NewAudio.Nodes;
using Serilog;

namespace NewAudio.Core
{
    public class AudioGraph : IDisposable
    {
        private readonly IList<IAudioNode> _nodes = new List<IAudioNode>();
        private readonly IList<AudioLink> _links = new List<AudioLink>();
        private readonly ILogger _logger;

        private int _nextId;

        public AudioGraph(ILogger logger)
        {
            _logger = logger;
        }

        public void Dispose()
        {
            _nextId = 0;
            _links.Clear();
            _nodes.Clear();
        }

        public int GetNextId()
        {
            return _nextId++;
        }

        public void PlayAll()
        {
            foreach (var node in _nodes)
            {
                // node.Config.Phase = LifecyclePhase.Playing;
            }
        }
        public void StopAll()
        {
            foreach (var node in _nodes)
            {
                // node.Config.Phase = LifecyclePhase.Stopped;
            }
        }

        
        public void AddLink(AudioLink link)
        {
            _links.Add(link);
            _logger.Debug("Added link {node}", link);
        }

        public void RemoveLink(AudioLink link)
        {
            _links.Remove(link);
            _logger.Debug("Removed link {node}", link);
        }

        public void AddNode(IAudioNode node) 
        {
            _nodes.Add(node);
            _logger.Debug("Added node {node}", node);
        }

        public void RemoveNode(IAudioNode node)
        {
            _nodes.Remove(node);
            _logger.Debug("Removed node {node}", node);
        }

        public string DebugInfo()
        {
            return $"Nodes: {_nodes.Count}, Links: {_links.Count}";
        }
    }
}