using System;
using System.Collections.Generic;
using System.Text;
using NewAudio.Nodes;
using Serilog;
using VL.Core;
using VL.Lib.Adaptive;
using VL.Lib.Basics.Resources;
using VL.Model;
using IList = System.Collections.IList;

namespace NewAudio.Core
{
    public class AudioGraph : IDisposable
    {
        private readonly IResourceHandle<AudioService> _audioService;
        private readonly IList<IAudioNode> _nodes = new List<IAudioNode>();
        private readonly IList<AudioLink> _links = new List<AudioLink>();
        private readonly ILogger _logger;
        private bool _playing;
        private int _nextId;
        private ulong _lastFrame;
        
        public int BufferSize { get; private set; }
        public int BufferCount { get; private set; }

        public AudioGraph() : this(Factory.Instance)
        {
        }

        public AudioGraph(IFactory api)
        {
            _audioService = api.GetAudioService();
            _logger = _audioService.Resource.GetLogger<AudioGraph>();
            _nextId = _audioService.Resource.GetNextId() * 1 >> 10;
        }

        public ILogger GetLogger<T>()
        {
            return _audioService.Resource.GetLogger<T>();
        }
        public void Update(bool playing, int bufferSize = 512, int buffersCount = 6)
        {
            var currentFrame = VLSession.Instance.UserRuntime.Frame;

            if (currentFrame != _lastFrame)
            {
                _lastFrame = currentFrame;
                if (playing != _playing)
                {
                    _playing = playing;
                    if (playing)
                    {
                        PlayAll();
                    }
                    else
                    {
                        StopAll();
                    }
                }
            }
        }
        
        public int GetNextId()
        {
            return _nextId++;
        }

        public void PlayAll()
        {
            _logger.Information("Starting all audio nodes");
            foreach (var node in _nodes)
            {
                node.PlayParams.Playing.Value = true;
            }
        }
        public void StopAll()
        {
            _logger.Information("Stopping all audio nodes");
            foreach (var node in _nodes)
            {
                node.PlayParams.Playing.Value = false;
            }
        }
        
        public void AddLink(AudioLink link)
        {
            _links.Add(link);
            _logger.Verbose("Added link {node}", link);
        }

        public void RemoveLink(AudioLink link)
        {
            _links.Remove(link);
            _logger.Verbose("Removed link {node}", link);
        }

        public int AddNode(IAudioNode node)
        {
            _nodes.Add(node);
            _logger.Verbose("Added node {node}", node);
            return _nodes.Count;
        }

        public void RemoveNode(IAudioNode node)
        {
            _nodes.Remove(node);
            _logger.Verbose("Removing node {node}", node);
        }
        
        public string DebugInfo()
        {
            var nodes = new StringBuilder("");
            foreach (var node in _nodes)
            {
                var debugInfo = node.DebugInfo();
                if (debugInfo != null)
                {
                    nodes.Append(", ").Append(debugInfo);
                }
            }
            return $"Nodes: {_nodes.Count}, Links: {_links.Count}{nodes}";
        }
        
        public void Dispose() => Dispose(true);
        
        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            _logger.Information("Dispose called for AudioGraph {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _audioService.Dispose();
                }

                _disposedValue = disposing;
            }
        }

        
    }
}