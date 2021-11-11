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
    public class AudioNodeInitParams: AudioParams
    {

    }
    public class AudioNodePlayParams: AudioParams
    {
        public AudioParam<bool> Playing;
        public AudioParam<AudioLink> Input;
        public AudioParam<int> BufferSize;
    }

    public interface IAudioNode : IDisposable, ILifecycleDevice
    {
        public AudioNodeInitParams InitParams { get; }
        public AudioNodePlayParams PlayParams { get; }
        public string DebugInfo();
        public IEnumerable<string> ErrorMessages();

    }
    public abstract class AudioNode<TInit, TPlay>: IAudioNode where TInit: AudioNodeInitParams where TPlay: AudioNodePlayParams
    {
        
        private List<Exception> _exceptions = new();
        public TInit InitParams { get; }
        public TPlay PlayParams { get; }
        AudioNodeInitParams IAudioNode.InitParams => InitParams;
        AudioNodePlayParams IAudioNode.PlayParams => PlayParams;
        public AudioLink Output { get; } = new();

        private ITargetBlock<AudioDataMessage> _targetBlock;
        private IDisposable _currentInputLink;
        private IDisposable _currentOutputLink;
        private BufferBlock<AudioDataMessage> _bufferBlock;
        public LifecyclePhase Phase { get; set; }
        public readonly LifecycleStateMachine Lifecycle;
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

        protected AudioNode() : this(VLApi.Instance){}

        private AudioNode(IVLApi api)
        {
            _graph = api.GetAudioGraph();
            Graph.AddNode(this);
            Logger = Graph.GetLogger<AudioNode<TInit, TPlay>>();
            
            Lifecycle = new LifecycleStateMachine(this);
            InitParams = (TInit)Activator.CreateInstance(typeof(TInit));
            PlayParams = (TPlay)Activator.CreateInstance(typeof(TPlay));
            PlayParams.BufferSize.Value = 4;
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
                Logger.Error("{e}", exception);
                _exceptions.Add(exception);
                // throw exception;
            }
        }

        protected AudioLink Update()
        {
            // TODO
            // if (BaseConfig.Reset)
            // {
                // if (Config.Input!=null)
                // {
                    // OnDisconnect(Config.Input);
                    // Config.Input = null;
                // }
            // }

            if (PlayParams.HasChanged)
            {
               
            }
            if( InitParams.HasChanged){
                InitParams.Commit();
                Lifecycle.EventHappens(LifecycleEvents.eInit);
            }

    
            if (PlayParams.Input.HasChanged)
            {
                if (_currentInputLink != null && PlayParams.Input.LastValue != null)
                {
                    _currentInputLink.Dispose();
                    _currentInputLink = null;
                } else if (_currentInputLink != null || PlayParams.Input.LastValue != null)
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
                PlayParams.BufferSize.Commit();
                CreateBuffer();
            }

            if (PlayParams.HasChanged)
            {
                PlayParams.Commit();
                Lifecycle.EventHappens(PlayParams.Playing.Value ? LifecycleEvents.ePlay : LifecycleEvents.eStop);
            } else if (IsInitValid() && IsPlayValid() && PlayParams.Playing.Value && Phase != LifecyclePhase.Play)
            {
                Lifecycle.EventHappens(PlayParams.Playing.Value ? LifecycleEvents.ePlay : LifecycleEvents.eStop);
            }

            return Output;
        }
        private void CreateBuffer()
        {
            _currentInputLink?.Dispose();
            _currentOutputLink?.Dispose();

            _bufferBlock = new BufferBlock<AudioDataMessage>(new DataflowBlockOptions()
            {
                BoundedCapacity = PlayParams.BufferSize.Value
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
            return  $"Buffer usage={_bufferBlock?.Count}";
        }

        public IEnumerable<string> ErrorMessages()
        {
            return _exceptions.Select(i=>i.Message);
        }

        public abstract Task<bool> Init();
        public abstract Task<bool> Free();
        public abstract bool Play();
        public abstract bool Stop();

        public virtual bool IsInitValid()
        {
            return true;
        }
        public virtual bool IsPlayValid()
        {
            return true;
        }

        private bool _disposedValue;
        
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for AudioNode {t} ({d})", this, disposing);
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