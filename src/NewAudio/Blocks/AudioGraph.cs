using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;

namespace NewAudio.Block
{
    public class AudioGraph : IDisposable
    {
        private readonly IAudioService _audioService = Resources.GetAudioService();
        private readonly ILogger _logger = Resources.GetLogger<AudioGraph>();
        public string GraphId { get; }
        private bool _wasEnabledBeforeParamChange;

        public AudioGraph()
        {
            GraphId = _audioService.RegisterAudioGraph(BeforeDeviceConfigChange, AfterDeviceConfigChange,
                BeforeAudioBufferFill, AfterAudioBufferFill);
            _logger.Information("-----------------------------------------");
            _logger.Information("AudioGraph initialized, id={GraphId}", GraphId);
        }

        private void BeforeDeviceConfigChange(DeviceState device)
        {
            _wasEnabledBeforeParamChange = IsEnabled;
            Disable();
            UninitializeAllNodes();
        }

        private void AfterDeviceConfigChange(DeviceState device)
        {
            SampleRate = device.Format.SampleRate;
            FramesPerBlock = device.Format.FramesPerBlock;

            InitializeAllNodes();
            SetEnabled(_wasEnabledBeforeParamChange);
        }

        private void BeforeAudioBufferFill(int numFrames)
        {
            PreProcess();
        }

        private void AfterAudioBufferFill(int numFrames)
        {
            PostProcess(numFrames);
        }

        public ulong NumberOfProcessedFrames { get; private set; }
        public double NumberOfProcessedSeconds => NumberOfProcessedFrames / (double)SampleRate;
        public int SampleRate { get; private set; }
        public int FramesPerBlock { get; private set; }
        public bool IsEnabled { get; private set; }
        public double TimeDuringLastProcessLoop { get; private set; }
        public HashSet<AudioBlock> AutoPulledNodes { get; } = new();
        public int AudioThreadId { get; set; }
        private Stopwatch _stopwatch = Stopwatch.StartNew();
        private List<OutputBlock> _outputBlocks = new();

        public void AddOutput(OutputBlock block)
        {
            _outputBlocks.Add(block);
            InitializeAllNodes();
        }

        public void RemoveOutput(OutputBlock block)
        {
            _outputBlocks.Remove(block);
        }

        public void Enable()
        {
            if (IsEnabled || _outputBlocks.Count == 0)
            {
                return;
            }

            foreach (var block in _outputBlocks.Where(block => !block.IsInitialized))
            {
                block.DoInitialize();
            }


            IsEnabled = true;
            foreach (var block in _outputBlocks)
            {
                block.Enable();
            }
        }

        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            foreach (var block in _outputBlocks)
            {
                block.Disable();
            }

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
            foreach (var block in _outputBlocks)
            {
                InitRecursive(block, traversed);
            }

            foreach (var node in AutoPulledNodes)
            {
                InitRecursive(node, traversed);
            }
        }

        public void UninitializeAllNodes()
        {
            var traversed = new HashSet<AudioBlock>();
            foreach (var block in _outputBlocks)
            {
                UninitRecursive(block, traversed);
            }

            foreach (var node in AutoPulledNodes)
            {
                UninitRecursive(node, traversed);
            }
        }

        public void DisconnectAllNodes()
        {
            var traversed = new HashSet<AudioBlock>();

            foreach (var block in _outputBlocks)
            {
                DisconnectRecursive(block, traversed);
            }

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

        public void PostProcess(int numFrames)
        {
            // todo
            // ProcessAutoPullNodes();
            IncrementFrameCount(numFrames);
            _stopwatch.Stop();
            TimeDuringLastProcessLoop = _stopwatch.Elapsed.Seconds;
        }

        private void IncrementFrameCount(int numFrames)
        {
            NumberOfProcessedFrames += (ulong)numFrames;
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
                    UninitializeAllNodes();
                }

                _disposedValue = disposing;
            }
        }
    }
}