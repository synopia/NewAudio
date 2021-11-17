using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Core;
using NewAudio.Dsp;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public enum ChannelMode
    {
        Specified,
        FromInput,
        FromOutput
    }
    public struct AudioNodeConfig
    {
        public int Channels;
        public ChannelMode ChannelMode;
        public bool IsAutoEnable;
        public bool IsAutoEnableSet;
    }

    public abstract class AudioNode : IDisposable
    {
        // public AudioParam<AudioLink> Input;
        // public AudioParam<AudioFormat> InputFormat;
        // public AudioParam<int> BufferSize;
        private readonly IResourceHandle<AudioGraph> _graph;
        public ILogger Logger { get; private set; }
        public AudioGraph Graph => _graph.Resource;
        public abstract string NodeName { get; }
        public int Id { get; }

        public bool IsEnabled => _enabled;
        public int NumberOfConnectedInputs => _inputs.Count;
        public int NumberOfConnectedOutputs => _outputs.Count;
        public int FramesPerBlock => Graph.FramesPerBlock;
        public int SampleRate => Graph.SampleRate;
        public int NumberOfChannels
        {
            get => _numberOfChannels;
            protected set
            {
                if (_numberOfChannels == value)
                {
                    return;
                }
                DoUnInitialize();
                _numberOfChannels = value;
            }
        }

        public ChannelMode ChannelMode
        {
            get => _channelMode;
            protected set => _channelMode = value;
        }

        public int MaxNumberOfInputChannels => _inputs.Max(i => i.NumberOfChannels);

        public bool AutoEnabled { get; set; }
        public bool IsInitialized => _initialized;
        public bool IsProcessesInPlace => _processInPlace;
        public DynamicAudioBuffer SummingBuffer => _summingBuffer;

        private List<Exception> _exceptions = new();
        public AudioLink Output { get; } = new();
        

        // private ITargetBlock<AudioDataMessage> _targetBlock;
        // private IDisposable _currentInputLink;
        // private IDisposable _currentOutputLink;
        // private BufferBlock<AudioDataMessage> _bufferBlock;

        private bool _enabled;
        private bool _initialized;
        private bool _processInPlace;
        private ChannelMode _channelMode;
        private int _numberOfChannels;

        private ulong _lastProcessedFrame;
        protected DynamicAudioBuffer _summingBuffer;
        protected DynamicAudioBuffer _internalBuffer;

        private List<AudioNode> _inputs = new();
        private List<AudioNode> _outputs = new();

        public List<AudioNode> Inputs => _inputs;
        /*
        protected ITargetBlock<AudioDataMessage> TargetBlock
        {
            set
            {
                if (_currentOutputLink != null)
                {
                    _currentOutputLink.Dispose();
                    _currentOutputLink = null;
                }

                _targetBlock = value;
                if (_targetBlock != null)
                {
                    _currentOutputLink = _bufferBlock.LinkTo(_targetBlock);
                }
            }
        }
        */

        protected AudioNode(AudioNodeConfig config)
        {
            _graph = Factory.GetAudioGraph();
            Id = Graph.AddNode(this);
            Logger = Graph.GetLogger<AudioNode>();
            
            ChannelMode = config.ChannelMode;
            NumberOfChannels = 1;
            AutoEnabled = true;
            _processInPlace = true;
            _initialized = false;
            _enabled = false;
            
            if (config.Channels > 0)
            {
                NumberOfChannels = config.Channels;
                ChannelMode = ChannelMode.Specified;
            }

            if (config.IsAutoEnableSet)
            {
                AutoEnabled = config.IsAutoEnable;
            }
            CreateBuffer();
        }

        protected void InitLogger<T>()
        {
            Logger = Graph.GetLogger<T>();
        }

        protected void ExceptionHappened(Exception exception, string method)
        {
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
                Logger.Error(exception, "Exceptions happened in {This}.{Method}", this, method);
                _exceptions.Add(exception);
                throw exception;
            }
        }

        public bool CanConnectToInput(AudioNode input)
        {
            if (input == null || input == this)
            {
                return false;
            }

            if (IsConnectedToInput(input))
            {
                return false;
            }

            return true;
        }
        
        public bool IsConnectedToInput(AudioNode other)
        {
            return _inputs.Contains(other);
        }
        public bool IsConnectedToOutput(AudioNode other)
        {
            return _outputs.Contains(other);
        }

        public void Enable()
        {
            if (!_initialized)
            {
                DoInitialize();
            }

            if (_enabled)
            {
                return;
            }
            
            _enabled = true;
            EnableProcessing();
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }
            
            _enabled = false;
            DisableProcessing();
        }

        public virtual void Connect(AudioNode output)
        {
            if (output == null || !output.CanConnectToInput(this))
            {
                return;
            }
            _outputs.Add(output);
            output.ConnectInput(this);
            output.NotifyConnectionsDidChange();
        }

        public virtual void Disconnect(AudioNode output)
        {
            if (output == null)
            {
                return;
            }

            _outputs.Remove(output);
            output.DisconnectInput(this);
            output.NotifyConnectionsDidChange();
        }

        public virtual void DisconnectAll()
        {
            DisconnectAllInputs();
            DisconnectAllOutputs();
        }

        public virtual void DisconnectAllOutputs()
        {
            var nodes = _outputs.ToArray();
            foreach (var node in nodes)
            {
                Disconnect(node);
            }
        }

        public virtual void DisconnectAllInputs()
        {
            foreach (var node in _inputs)
            {
                node.DisconnectOutput(this);
            }
            _inputs.Clear();
            NotifyConnectionsDidChange();
        }

        protected virtual void Initialize() {}
        protected virtual void UnInitialize() {}
        protected virtual void EnableProcessing(){}
        protected virtual void DisableProcessing(){}
        protected virtual void Process(AudioBuffer buffer){}

        protected virtual bool SupportsProcessInPlace()
        {
            return true;
        }

        protected virtual bool SupportsInputNumberOfChannels(int numberOfChannels)
        {
            return _numberOfChannels==numberOfChannels;
        }

        protected virtual void ConnectInput(AudioNode input)
        {
            // todo lock
            _inputs.Add(input);
            ConfigureConnections();
        }

        protected virtual void DisconnectInput(AudioNode input)
        {
            // todo lock
            _inputs.Remove(input);
        }

        protected virtual void DisconnectOutput(AudioNode output)
        {
            // todo lock
            _outputs.Remove(output);
        }

        protected bool InputChannelsAreUnequal()
        {
            if (_inputs.Count > 0)
            {
                var numChannels = _inputs[0].NumberOfChannels;
                return _inputs.Any(i => i.NumberOfChannels != numChannels);
            }

            return false;
        }
        public virtual void ConfigureConnections()
        {
            _processInPlace = SupportsProcessInPlace();
            if (NumberOfConnectedInputs > 1 || NumberOfConnectedOutputs > 1)
            {
                _processInPlace = false;
            }

            bool unequalInputs = InputChannelsAreUnequal();
            foreach (var input in _inputs)
            {
                bool inputProcessInPlace = true;
                int inputNumberOfChannels = input.NumberOfChannels;
                if (!SupportsInputNumberOfChannels(inputNumberOfChannels))
                {
                    if (_channelMode == ChannelMode.FromInput)
                    {
                        NumberOfChannels = inputNumberOfChannels;
                    } else if (_channelMode == ChannelMode.FromOutput)
                    {
                        input.NumberOfChannels = _numberOfChannels;
                        input.ConfigureConnections();
                    }
                    else
                    {
                        _processInPlace = false;
                        inputProcessInPlace = false;
                    }
                }

                if (input.IsProcessesInPlace && input.NumberOfConnectedOutputs > 1)
                {
                    inputProcessInPlace = false;
                }

                if (unequalInputs)
                {
                    inputProcessInPlace = false;
                }

                if (!inputProcessInPlace)
                {
                    input.SetupProcessWithSumming();
                }
                
                input.DoInitialize();
            }
            
            foreach (var output in _outputs)
            {
                if (!output.SupportsInputNumberOfChannels(_numberOfChannels))
                {
                    if (output.ChannelMode == ChannelMode.FromInput)
                    {
                        output.NumberOfChannels = _numberOfChannels;
                        output.ConfigureConnections();
                    }
                    else
                    {
                        _processInPlace = false;
                    }
                }
            }

            if (!_processInPlace)
            {
                SetupProcessWithSumming();
            }
            
            DoInitialize();

        }

        protected void NotifyConnectionsDidChange()
        {
            // todo inform graph
            Graph.ConnectionsDidChange(this);
        }

        public void DoInitialize()
        {
            if (_initialized)
            {
                return;
            }

            if (_processInPlace && !SupportsProcessInPlace())
            {
                SetupProcessWithSumming();
            }
            
            Initialize();
            _initialized = true;
            if (AutoEnabled)
            {
                Enable();
            }
        }

        public  void DoUnInitialize()
        {
            if (!_initialized)
            {
                return;
            }

            if (AutoEnabled)
            {
                Disable();
            }
            UnInitialize();
            _initialized = false;
        }

        protected void PullInputs(AudioBuffer inPlaceBuffer)
        {
            if (_processInPlace)
            {
                if (_inputs.Count == 0)
                {
                    inPlaceBuffer.Zero();
                    if (_enabled)
                    {
                        Process(inPlaceBuffer);
                    }
                }
                else
                {
                    var input = _inputs[0];
                    input.PullInputs(inPlaceBuffer);
                    if (!input.IsProcessesInPlace)
                    {
                        MixBuffers.MixBuffer(input._internalBuffer, inPlaceBuffer);
                    }

                    if (_enabled)
                    {
                        Process(inPlaceBuffer);
                    }
                }
            }
            else
            {
                ulong lastProcessedFrame = _graph.Resource.LastProcessedFrame;
                if (lastProcessedFrame != _lastProcessedFrame)
                {
                    _lastProcessedFrame = lastProcessedFrame;
                    _summingBuffer.Zero();
                    SumInputs();
                }
            }
        }

        protected void SumInputs()
        {
            foreach (var input in _inputs)
            {
                input.PullInputs(_internalBuffer);
                var processedBuffer = input.IsProcessesInPlace ? _internalBuffer : input._internalBuffer;
                MixBuffers.SumMixBuffer(processedBuffer, _summingBuffer);
            }

            if (_enabled)
            {
                Process(_summingBuffer);
            }
            
            MixBuffers.MixBuffer(_summingBuffer, _internalBuffer);
        }

        protected void SetupProcessWithSumming()
        {
            _processInPlace = false;
            
            _internalBuffer.SetSize(FramesPerBlock, NumberOfChannels);
            _summingBuffer.SetSize(FramesPerBlock, NumberOfChannels);
        }
        
        /*
        protected AudioLink Update(AudioParams p)
        {
            try
            {
                if (PlayConfig.Input.HasChanged)
                {
                    if (_currentInputLink != null && PlayConfig.Input.LastValue != null)
                    {
                        _currentInputLink.Dispose();
                        _currentInputLink = null;
                    }
                    else if (_currentInputLink != null || PlayConfig.Input.LastValue != null)
                    {
                        Logger.Warning("Illegal input link found!");
                        _currentInputLink?.Dispose();
                        _currentInputLink = null;
                    }

                    if (PlayConfig.Input.Value != null)
                    {
                        _currentInputLink = PlayConfig.Input.Value.SourceBlock.LinkTo(_bufferBlock);
                    }
                }

                if (PlayConfig.BufferSize.HasChanged)
                {
                    CreateBuffer();
                    PlayConfig.BufferSize.Commit();
                }

                if (PlayConfig.HasChanged)
                {
                    PlayConfig.Reset.Value = false;
                    Stop();

                    if (PlayConfig.Phase.Value == LifecyclePhase.Play)
                    {
                        var valid = Play();
                        if (!valid)
                        {
                            PlayConfig.Phase.Value = LifecyclePhase.Invalid;
                        }
                    }

                    // if (PlayParams.Phase.Value == LifecyclePhase.Stop)
                    // {
                    // Stop();
                    // }

                    PlayConfig.Commit();
                }

            }
            catch (Exception e)
            {
                _currentInputLink?.Dispose();
                _currentInputLink = null;
                Output.SourceBlock = null;
                ExceptionHappened(e, "AudioNode.Update");
            }
            finally
            {
                p.Commit();
            }
            return Output;
        }
        */

        private void CreateBuffer()
        {
            _internalBuffer = new DynamicAudioBuffer();
            _summingBuffer = new DynamicAudioBuffer();
            
            /*
            _currentInputLink?.Dispose();
            _currentOutputLink?.Dispose();
            _currentInputLink = null;
            _currentOutputLink = null;
            
            _bufferBlock = new BufferBlock<AudioDataMessage>(new DataflowBlockOptions()
            {
                BoundedCapacity = Math.Max(1, PlayConfig.BufferSize.Value)
            });
            if (PlayConfig.Input.Value != null)
            {
                _currentInputLink = PlayConfig.Input.Value.SourceBlock.LinkTo(_bufferBlock);
            }

            if (_targetBlock != null)
            {
                _currentOutputLink = _bufferBlock.LinkTo(_targetBlock);
            }
        */
        }

        public virtual string DebugInfo()
        {
            return $"Buffer";
        }

        public override string ToString()
        {
            return $"{NodeName} ({Id})";
        }

        public IEnumerable<string> ErrorMessages()
        {
            return _exceptions.Select(i => i.Message);
        }

        /*
        public abstract bool Play();
        public abstract void Stop();
        */

        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for AudioNode {This} ({Disposing})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // _currentInputLink?.Dispose();
                    Output.Dispose();
                    Graph.RemoveNode(this);
                    _graph.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}