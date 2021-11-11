using System;
using System.Collections.Generic;
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

        public AudioGraph() : this(VLApi.Instance)
        {
        }

        public AudioGraph(IVLApi api)
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
                        _logger.Information("Starting audio");
                        PlayAll();
                    }
                    else
                    {
                        _logger.Information("Stopping audio");
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
            foreach (var node in _nodes)
            {
                
                node.PlayParams.Playing.Value = true;
            }
        }
        public void StopAll()
        {
            foreach (var node in _nodes)
            {
                node.PlayParams.Playing.Value = false;
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
            _logger.Debug("Removing node {node}", node);
        }
        
        public string DebugInfo()
        {
            var nodes = "";
            foreach (var node in _nodes)
            {
                var debugInfo = node.DebugInfo();
                if (debugInfo != null)
                {
                    nodes += $", {debugInfo}";
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