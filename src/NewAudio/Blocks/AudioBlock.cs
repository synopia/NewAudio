using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewAudio.Core;
using NewAudio.Dsp;
using Serilog;
using VL.Lib.Basics.Resources;
using VL.NewAudio;

namespace NewAudio.Block
{
    public enum ChannelMode
    {
        Specified,
        MatchesInput,
        MatchesOutput
    }
    public class AudioBlockFormat
    {
        public int Channels;
        public ChannelMode ChannelMode;
        private bool _autoEnable;
        public bool AutoEnable
        {
            get => _autoEnable;
            set
            {
                IsAutoEnableSet = true;
                _autoEnable = value;
            }
        }

        public bool IsAutoEnableSet;

        public AudioBlockFormat WithChannels(int channels)
        {
            Channels = channels;
            return this;
        }
        public AudioBlockFormat WithChannelMode(ChannelMode mode)
        {
            ChannelMode = mode;
            return this;
        }
        public AudioBlockFormat WithAutoEnable(bool b)
        {
            AutoEnable = b;
            IsAutoEnableSet = true;
            return this;
        }
    }

    public abstract class AudioBlock : IDisposable
    {
        protected ILogger Logger { get; private set; }
        private readonly IResourceHandle<AudioGraph> _graph;
        public abstract string Name { get; }


        public AudioGraph Graph => _graph.Resource;
        public bool IsEnabled { get; private set; }
        public bool IsInitialized { get; private set; }
        public IList<AudioBlock> Inputs { get; } = new List<AudioBlock>(); 
        public IList<AudioBlock> Outputs { get; } = new List<AudioBlock>();
        public int NumberOfConnectedInputs => Inputs.Count;
        public int NumberOfConnectedOutputs => Outputs.Count;
        public bool ProcessInPlace { get; private set; }
        public int MaxNumberOfInputChannels =>Inputs.Max(i => i.NumberOfChannels);
        public int SampleRate => Graph.SampleRate;
        public int FramesPerBlock => Graph.FramesPerBlock;
        public ChannelMode ChannelMode { get; protected set; }
        public bool IsAutoEnable { get; set; }

        protected DynamicAudioBuffer InternalBuffer { get; } = new();
        protected DynamicAudioBuffer MixingBuffer { get; } = new();
        private ulong _lastProcessedFrame;

        private int _numberOfChannels;
        public int NumberOfChannels
        {
            get => _numberOfChannels;
            protected set
            {
                if (_numberOfChannels == value)
                {
                    return;
                }
                DoUninitialize();
                _numberOfChannels = value;
            }
        }

        protected AudioBlock(AudioBlockFormat format)
        {
            IsInitialized = false;
            IsEnabled = false;
            ChannelMode = format.ChannelMode;
            NumberOfChannels = 1;
            IsAutoEnable = true;
            ProcessInPlace = true;
            _lastProcessedFrame = UInt64.MaxValue;
            
            _graph = Resources.GetAudioGraph();

            if (format.Channels> 0)
            {
                NumberOfChannels = format.Channels;
                ChannelMode = ChannelMode.Specified;
            }

            if (format.IsAutoEnableSet)
            {
                IsAutoEnable = format.AutoEnable;
            }
        }

        protected void InitLogger<T>()
        {
            Logger = Resources.GetLogger<T>();
        }

        public void SetEnabled(bool b)
        {
            if( b ){
                Enable();
            }
            else
            {
                Disable();
            }
        }

        public void Enable()
        {
            if (!IsInitialized)
            {
                DoInitialize();
            }

            if (IsEnabled)
            {
                return;
            }

            IsEnabled = true;
            EnableProcessing();
        }

        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            IsEnabled = false;
            DisableProcessing();
        }

        
        
        public bool CanConnectToInput(AudioBlock input)
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
        public bool IsConnectedToInput(AudioBlock input)
        {
            return Inputs.Contains(input);
        }
        public bool IsConnectedToOutput(AudioBlock output)
        {
            return Outputs.Contains(output);
        }
        public virtual void Connect(AudioBlock output)
        {
            if (output == null || !output.CanConnectToInput(this))
            {
                return;
            }
            Outputs.Add(output);
            output.ConnectInput(this);
            output.NotifyConnectionsDidChange();
        }

        public virtual void Disconnect(AudioBlock output)
        {
            if (output == null)
            {
                return;
            }

            Outputs.Remove(output);
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
            foreach (var node in Outputs.ToArray())
            {
                Disconnect(node);
            }
        }

        public virtual void DisconnectAllInputs()
        {
            foreach (var node in Inputs)
            {
                node.DisconnectOutput(this);
            }
            Inputs.Clear();
            NotifyConnectionsDidChange();
        }

        public void PullInputs(AudioBuffer inPlaceBuffer)
        {
            if (ProcessInPlace)
            {
                if (Inputs.Count == 0)
                {
                    inPlaceBuffer.Zero();
                    if (IsEnabled)
                    {
                        Process(inPlaceBuffer);
                    }
                }
                else
                {
                    var input = Inputs[0];
                    input.PullInputs(inPlaceBuffer);
                    if (!input.ProcessInPlace)
                    {
                        MixBuffers.MixBuffer(input.InternalBuffer, inPlaceBuffer);
                    }

                    if (IsEnabled)
                    {
                        Process(inPlaceBuffer);
                    }
                }
            }
            else
            {
                ulong numProcessedFrames = Graph.NumberOfProcessedFrames;
                if (numProcessedFrames != _lastProcessedFrame)
                {
                    _lastProcessedFrame = numProcessedFrames;
                    MixingBuffer.Zero();
                    MixInputs();
                }
            }
        }

        protected virtual void ConnectInput(AudioBlock input)
        {
            // todo lock
            Inputs.Add(input);
            ConfigureConnections();
        }

        protected virtual void DisconnectInput(AudioBlock input)
        {
            // todo lock
            Inputs.Remove(input);
        }
        protected virtual void DisconnectOutput(AudioBlock input)
        {
            // todo lock
            Outputs.Remove(input);
        }

        protected virtual void Initialize(){}
        protected virtual void Uninitialize(){}
        protected virtual void EnableProcessing(){}
        protected virtual void DisableProcessing(){}

        protected virtual void Process(AudioBuffer buffer)
        {
            
        }

        protected virtual void MixInputs()
        {
            foreach (var input in Inputs)
            {
                input.PullInputs(InternalBuffer);
                var processedBuffer = input.ProcessInPlace ? InternalBuffer : input.InternalBuffer;
                MixBuffers.SumMixBuffer(processedBuffer, MixingBuffer);
            }

            if (IsEnabled)
            {
                Process(MixingBuffer);
            }
            
            MixBuffers.MixBuffer(MixingBuffer, InternalBuffer);
        }

        protected virtual bool SupportsInputNumberChannels(int numChannels)
        {
            return NumberOfChannels == numChannels;
        }

        protected virtual bool SupportsProcessInPlace()
        {
            return true;
        }

        public void ConfigureConnections()
        {
            ProcessInPlace = SupportsProcessInPlace();
            if (NumberOfConnectedInputs > 1 || NumberOfConnectedOutputs > 1)
            {
                ProcessInPlace = false;
            }

            bool unequalInputs = InputChannelsAreUnequal();
            foreach (var input in Inputs)
            {
                bool inputProcessInPlace = true;
                int inputNumChannels = input.NumberOfChannels;
                if (!SupportsInputNumberChannels(inputNumChannels))
                {
                    if (ChannelMode == ChannelMode.MatchesInput)
                    {
                        NumberOfChannels = MaxNumberOfInputChannels;
                    } else if (ChannelMode == ChannelMode.MatchesOutput)
                    {
                        input.NumberOfChannels = NumberOfChannels;
                        input.ConfigureConnections();
                    }
                    else
                    {
                        ProcessInPlace = false;
                        inputProcessInPlace = false;
                    }
                }

                if (input.ProcessInPlace && input.NumberOfConnectedOutputs > 1)
                {
                    inputProcessInPlace = false;
                }

                if (unequalInputs)
                {
                    inputProcessInPlace = false;
                }

                if (!inputProcessInPlace)
                {
                    input.SetupProcessWithMixing();
                }
                
                input.DoInitialize();
            }
            foreach (var output in Outputs)
            {
                if (!output.SupportsInputNumberChannels(NumberOfChannels))
                {
                    if (output.ChannelMode == ChannelMode.MatchesInput)
                    {
                        output.NumberOfChannels = NumberOfChannels;
                        output.ConfigureConnections();
                    }
                    else
                    {
                        ProcessInPlace = false;
                    }
                }
            }

            if (!ProcessInPlace)
            {
                SetupProcessWithMixing();
            }
            DoInitialize();
        }

        protected void SetupProcessWithMixing()
        {
            ProcessInPlace = false;
            var frames = FramesPerBlock;
            InternalBuffer.SetSize(frames, NumberOfChannels);
            MixingBuffer.SetSize(frames, NumberOfChannels);
        }

        protected void NotifyConnectionsDidChange()
        {
            // todo inform context
        }

        protected bool InputChannelsAreUnequal()
        {
            if (Inputs.Count > 0)
            {
                var num = Inputs[0].NumberOfChannels;
                return Inputs.Any(i => i.NumberOfChannels != num);
            }

            return false;
        }

        public Tuple<int,int> FrameProcessRange { get; private set; }

        public void DoInitialize()
        {
            if (IsInitialized || FramesPerBlock==0 )
            {
                return;
            }

            if (ProcessInPlace && !SupportsProcessInPlace())
            {
                SetupProcessWithMixing();
            }

            FrameProcessRange = new Tuple<int, int>(0, FramesPerBlock);
            
            Initialize();
            IsInitialized = true;
            if (IsAutoEnable)
            {
                Enable();
            }
        }

        public void DoUninitialize()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (IsAutoEnable)
            {
                Disable();
            }

            Uninitialize();
            IsInitialized = false;
        }
        
        public override string ToString()
        {
            return Name;
        }

        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for AudioBlock {This} ({Disposing})", Name, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    DisconnectAll();
                    InternalBuffer?.Dispose();
                    MixingBuffer?.Dispose();
                    _graph.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
    
}