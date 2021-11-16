using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public class AudioNodeParams : AudioParams
    {
        public AudioParam<bool> Reset;
        public AudioParam<LifecyclePhase> Phase;
        public AudioParam<AudioLink> Input;
        public AudioParam<AudioFormat> InputFormat;
        public AudioParam<int> BufferSize;

        public void Update(AudioLink input, bool reset=false, int bufferSize=1, LifecyclePhase phase=LifecyclePhase.Uninitialized)
        {
            Input.Value = input;
            InputFormat.Value = input?.Format ?? default;
            Reset.Value = reset;
            BufferSize.Value = bufferSize;
            if (phase != LifecyclePhase.Uninitialized)
            {
                Phase.Value = phase;
            }
            else
            {
                Phase.Value = LifecyclePhase.Play;
            }
        }
    }

    public interface IAudioNode : IDisposable
    {
        ILogger Logger { get; }
        public AudioNodeParams PlayParams { get; }
        public string DebugInfo();
        public IEnumerable<string> ErrorMessages();
    }

    public abstract class AudioNode : IAudioNode
    {
        public abstract string NodeName { get; }
        public int Id { get; }

        private List<Exception> _exceptions = new();
        public AudioNodeParams PlayParams { get; }
        public AudioLink Output { get; } = new();

        private ITargetBlock<AudioDataMessage> _targetBlock;
        private IDisposable _currentInputLink;
        private IDisposable _currentOutputLink;
        private BufferBlock<AudioDataMessage> _bufferBlock;
        public LifecyclePhase Phase => PlayParams.Phase.Value;
        private readonly IResourceHandle<AudioGraph> _graph;
        public AudioGraph Graph => _graph.Resource;
        public ILogger Logger { get; private set; }

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

        protected AudioNode()
        {
            _graph = Factory.GetAudioGraph();
            Id = Graph.AddNode(this);
            Logger = Graph.GetLogger<AudioNode>();

            PlayParams = AudioParams.Create<AudioNodeParams>();
            PlayParams.BufferSize.Value = 1;
            PlayParams.BufferSize.Commit();
            CreateBuffer();
        }

        protected void InitLogger<T>()
        {
            Logger = Graph.GetLogger<T>();
        }


        public void ExceptionHappened(Exception exception, string method)
        {
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
                Logger.Error(exception, "Exceptions happened in {This}.{Method}", this, method);
                _exceptions.Add(exception);
                throw exception;
            }
        }

        protected AudioLink Update(AudioParams p)
        {
            try
            {
                if (PlayParams.Input.HasChanged)
                {
                    if (_currentInputLink != null && PlayParams.Input.LastValue != null)
                    {
                        _currentInputLink.Dispose();
                        _currentInputLink = null;
                    }
                    else if (_currentInputLink != null || PlayParams.Input.LastValue != null)
                    {
                        Logger.Warning("Illegal input link found!");
                        _currentInputLink?.Dispose();
                        _currentInputLink = null;
                    }

                    if (PlayParams.Input.Value != null)
                    {
                        _currentInputLink = PlayParams.Input.Value.SourceBlock.LinkTo(_bufferBlock);
                    }
                }

                if (PlayParams.BufferSize.HasChanged)
                {
                    CreateBuffer();
                    PlayParams.BufferSize.Commit();
                }

                if (PlayParams.HasChanged)
                {
                    PlayParams.Reset.Value = false;
                    Stop();

                    if (PlayParams.Phase.Value == LifecyclePhase.Play)
                    {
                        var valid = Play();
                        if (!valid)
                        {
                            PlayParams.Phase.Value = LifecyclePhase.Invalid;
                        }
                    }

                    // if (PlayParams.Phase.Value == LifecyclePhase.Stop)
                    // {
                    // Stop();
                    // }

                    PlayParams.Commit();
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

        private void CreateBuffer()
        {
            _currentInputLink?.Dispose();
            _currentOutputLink?.Dispose();
            _currentInputLink = null;
            _currentOutputLink = null;
            
            _bufferBlock = new BufferBlock<AudioDataMessage>(new DataflowBlockOptions()
            {
                BoundedCapacity = Math.Max(1, PlayParams.BufferSize.Value)
            });
            if (PlayParams.Input.Value != null)
            {
                _currentInputLink = PlayParams.Input.Value.SourceBlock.LinkTo(_bufferBlock);
            }

            if (_targetBlock != null)
            {
                _currentOutputLink = _bufferBlock.LinkTo(_targetBlock);
            }
        }

        public virtual string DebugInfo()
        {
            return $"Buffer: {_bufferBlock?.Count}";
        }

        public override string ToString()
        {
            return $"{NodeName} ({Id})";
        }

        public IEnumerable<string> ErrorMessages()
        {
            return _exceptions.Select(i => i.Message);
        }

        public abstract bool Play();
        public abstract void Stop();

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
                    _currentInputLink?.Dispose();
                    Output.Dispose();
                    Graph.RemoveNode(this);
                    _graph.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}