using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Block
{

    public class AudioGraph : IDisposable
    {
        private readonly IResourceHandle<AudioService> _audioService;
        private readonly ILogger _logger;
        
        private int _nextId;


        public AudioGraph()
        {
            _audioService = Factory.GetAudioService();
            _logger = _audioService.Resource.GetLogger<AudioGraph>();
            _nextId = (_audioService.Resource.GetNextId()) << 10;
            _logger.Information("-----------------------------------------");
            _logger.Information("AudioGraph initialized, id={Id}", _nextId);
        }

        public IDevice OutputDevice { get; set; }
        public ulong NumberOfProcessedFrames { get; private set; }
        public double NumberOfProcessedSeconds =>NumberOfProcessedFrames/(double)SampleRate;
        public int SampleRate => OutputBlock?.OutputSampleRate ?? 0;
        public int FramesPerBlock => OutputBlock?.OutputFramesPerBlock ?? 0;
        public bool IsEnabled { get; private set; }
        public double TimeDuringLastProcessLoop { get; private set; }
        public HashSet<AudioBlock> AutoPulledNodes { get; } = new();
        public int AudioThreadId { get; set; }
        private Stopwatch _stopwatch = Stopwatch.StartNew();
        private OutputBlock _outputBlock;

        public OutputBlock OutputBlock
        {
            get
            {
                return _outputBlock;
            }
            set
            {
                if (_outputBlock != null)
                {
                    if (value != null && (_outputBlock.FramesPerBlock != value.FramesPerBlock ||
                                          _outputBlock.SampleRate != value.SampleRate))
                    {
                        UninitializeAllNodes();
                    }
                    else
                    {
                        UninitializeNode(_outputBlock);
                    }
                }
                _outputBlock = value;
                if (_outputBlock!=null)
                {
                    InitializeAllNodes();
                }
            }
        }

        public ILogger GetLogger<T>()
        {
            return _audioService.Resource.GetLogger<T>();
        }

        public int GetNextId()
        {
            return _nextId++;
        }


        public void Enable()
        {
            if (IsEnabled || OutputBlock==null)
            {
                return;
            }

            if (!OutputBlock.IsInitialized)
            {
                OutputBlock.DoInitialize();
            }

            IsEnabled = true;
            OutputBlock.Enable();
        }

        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            OutputBlock?.Disable(); 
            
            IsEnabled = false;
        }

        public void SetEnabled(bool b)
        {
            if (b)
            {
                Enable();
            }
            else
            {
                Disable();
            }
        }

        public void ConnectionsDidChange(AudioBlock block)
        {
            
        }

        public void InitializeNode(AudioBlock block)
        {
            block.DoInitialize();
        }
        public void UninitializeNode(AudioBlock block)
        {
            block.DoUninitialize();
        }

        public void InitializeAllNodes()
        {
            var traversed = new HashSet<AudioBlock>();
            InitRecursive(OutputBlock, traversed);
            foreach (var node in AutoPulledNodes)
            {
                InitRecursive(node, traversed);
            }
        }
        
        public void UninitializeAllNodes()
        {
            var traversed = new HashSet<AudioBlock>();
            UninitRecursive(OutputBlock, traversed);
            foreach (var node in AutoPulledNodes)
            {
                UninitRecursive(node, traversed);
            }
        }

        public void DisconnectAllNodes()
        {
            var traversed = new HashSet<AudioBlock>();

            DisconnectRecursive(OutputBlock, traversed);
            foreach (var node in AutoPulledNodes)
            {
                DisconnectRecursive(node, traversed);
            }
        }

        public void AddAutoPullNode(AudioBlock block)
        {
            // AutoPulledNodes.Add(node);
        }

        public void RemoveAutoPullNode(AudioBlock block)
        {
            
        }

        public bool IsAudioThread()
        {
            return AudioThreadId == Thread.CurrentThread.ManagedThreadId;
        }

        

        public void PreProcess()
        {
            _stopwatch.Restart();
            AudioThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public void PostProcess()
        {
            // todo
            // ProcessAutoPullNodes();
            IncrementFrameCount();
            _stopwatch.Stop();
            TimeDuringLastProcessLoop = _stopwatch.Elapsed.Seconds;
        }

        private void IncrementFrameCount()
        {
            NumberOfProcessedFrames += (ulong)FramesPerBlock;
        }
        
        
        public string DebugInfo()
        {
            return null;
        }


        protected void DisconnectRecursive(AudioBlock block, HashSet<AudioBlock> traversed)
        {
            if (block == null || traversed.Contains(block))
            {
                return;
            }

            traversed.Add(block);
            foreach (var input in block.Inputs)
            {
                DisconnectRecursive(input, traversed);
            }
            
            block.DisconnectAllInputs();
        }
        protected void InitRecursive(AudioBlock block, HashSet<AudioBlock> traversed)
        {
            if (block == null || traversed.Contains(block))
            {
                return;
            }

            traversed.Add(block);
            foreach (var input in block.Inputs)
            {
                InitRecursive(input, traversed);
            }

            block.ConfigureConnections();
        }
        protected void UninitRecursive(AudioBlock block, HashSet<AudioBlock> traversed)
        {
            if (block == null || traversed.Contains(block))
            {
                return;
            }

            traversed.Add(block);
            foreach (var input in block.Inputs)
            {
                UninitRecursive(input, traversed);
            }

            block.DoUninitialize();
            
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