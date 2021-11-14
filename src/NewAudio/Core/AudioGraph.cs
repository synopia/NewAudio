using System;
using System.Collections.Generic;
using System.Text;
using NewAudio.Devices;
using NewAudio.Nodes;
using Serilog;
using VL.Lib.Basics.Resources;
using VL.Model;

namespace NewAudio.Core
{
    public class AudioGraph : IDisposable
    {
        private readonly IResourceHandle<AudioService> _audioService;
        private readonly IResourceHandle<DriverManager> _driverManager;
        private readonly IList<IAudioNode> _nodes = new List<IAudioNode>();
        private readonly IList<AudioLink> _links = new List<AudioLink>();
        private readonly ILogger _logger;
        private bool _playing;
        private int _nextId;
        private ulong _lastFrame;

        public AudioGraph() : this(Factory.Instance)
        {
        }

        public AudioGraph(IFactory api)
        {
            _audioService = api.GetAudioService();
            _driverManager = api.GetDriverManager();
            _logger = _audioService.Resource.GetLogger<AudioGraph>();
            _nextId = (_audioService.Resource.GetNextId() * 1) >> 10;
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
                try
                {
                    _driverManager.Resource.UpdateAllDevices();
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error in DriverManager");
                }
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
                node.PlayParams.Phase.Value = LifecyclePhase.Play;
            }
        }

        public void StopAll()
        {
            _logger.Information("Stopping all audio nodes");
            foreach (var node in _nodes)
            {
                node.PlayParams.Phase.Value = LifecyclePhase.Stop;
            }
        }

        public void AddLink(AudioLink link)
        {
            _links.Add(link);
            _logger.Verbose("Added link {@Node}", link);
        }

        public void RemoveLink(AudioLink link)
        {
            _links.Remove(link);
            _logger.Verbose("Removed link {@Node}", link);
        }

        public int AddNode(IAudioNode node)
        {
            _nodes.Add(node);
            _logger.Verbose("Added node {Node}", node);
            return _nodes.Count;
        }

        public void RemoveNode(IAudioNode node)
        {
            _nodes.Remove(node);
            _logger.Verbose("Removing node {Node}", node);
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

        public void Dispose()
        {
            Dispose(true);
        }

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            _logger.Information("Dispose called for AudioGraph ({Disposing})", disposing);
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