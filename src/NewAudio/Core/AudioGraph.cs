using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IList<AudioNode> _nodes = new List<AudioNode>();
        private readonly IList<AudioLink> _links = new List<AudioLink>();
        private readonly ILogger _logger;
        private bool _enabled;
        private int _nextId;
        public ulong LastProcessedFrame => _lastFrame;
        private ulong _lastFrame;
        public int SampleRate => OutputNode.OutputSampleRate;
        public int FramesPerBlock => OutputNode.OutputFramesPerBlock;
        private OutputNode _output;
        public OutputNode OutputNode
        {
            get => _output;
            set
            {
                if (_output!=null)
                {
                    if (value != null && (_output.OutputFramesPerBlock != value.OutputFramesPerBlock ||
                                          _output.OutputSampleRate != value.OutputSampleRate))
                    {
                        UnInitializeAllNodes();
                    }
                    else
                    {
                        UnInitializeNode(_output);
                    }
                }
                _output = value;
                if (_output != null)
                {
                    InitializeAllNodes();
                }
            }
        }

        public AudioGraph()
        {
            _audioService = Factory.GetAudioService();
            _driverManager = Factory.GetDriverManager();
            _logger = _audioService.Resource.GetLogger<AudioGraph>();
            _nextId = (_audioService.Resource.GetNextId()) << 10;
            Log.Logger.Information("-----------------------------------------");
            _logger.Information("AudioGraph initialized, id={Id}", _nextId);
        }

        public ILogger GetLogger<T>()
        {
            return _audioService.Resource.GetLogger<T>();
        }

        public void SetEnabled(bool enabled = true)
        {
            if (enabled)
            {
                Enable();
            }
            else
            {
                Disable();
            }
        }
        
        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

        
            if (!OutputNode.IsInitialized)
            {
                OutputNode.DoInitialize();
            }

            _enabled = true;
            OutputNode.Enable();
            
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _enabled = false;
            
                OutputNode.Disable();
        }
        
        public void ConnectionsDidChange(AudioNode node){}

        public void InitializeNode(AudioNode node)
        {
            node.DoInitialize();
        }
        public void UnInitializeNode(AudioNode node)
        {
            node.DoUnInitialize();
        }
        public void InitializeAllNodes()
        {
            var traversed = new HashSet<AudioNode>();
            
            InitRecursive(OutputNode, traversed);
        }
        public void UnInitializeAllNodes()
        {
            var traversed = new HashSet<AudioNode>();
            
            UnInitRecursive(OutputNode, traversed);
        }

        private void InitRecursive(AudioNode node, HashSet<AudioNode> traversed)
        {
            if (node == null || traversed.Contains(node))
            {
                return;
            }

            traversed.Add(node);
            foreach (var input in node.Inputs)
            {
                InitRecursive(input, traversed);
            }
            node.ConfigureConnections();
        }
        private void UnInitRecursive(AudioNode node, HashSet<AudioNode> traversed)
        {
            if (node == null || traversed.Contains(node))
            {
                return;
            }

            traversed.Add(node);
            foreach (var input in node.Inputs)
            {
                UnInitRecursive(input, traversed);
            }
            node.DoUnInitialize();
        }
        
        public bool Update(bool playing)
        {
            _audioService.Resource.Update();
            SetEnabled(playing);

            return playing;
        }

        public int GetNextId()
        {
            return _nextId++;
        }

        /*
        public void PlayAll()
        {
            _logger.Information("Starting all audio nodes");
            foreach (var node in _nodes)
            {
                node.PlayConfig.Phase.Value = LifecyclePhase.Play;
            }
        }

        public void StopAll()
        {
            _logger.Information("Stopping all audio nodes");
            foreach (var node in _nodes)
            {
                node.PlayConfig.Phase.Value = LifecyclePhase.Stop;
            }
        }
        */

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

        public int AddNode(AudioNode node)
        {
            _nodes.Add(node);
            _logger.Verbose("Added node {Node}", node);
            return _nodes.Count;
        }

        public void RemoveNode(AudioNode node)
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