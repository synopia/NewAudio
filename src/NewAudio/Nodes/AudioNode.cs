using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using VL.Core;
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
        private readonly ILogger _logger = AudioService.Instance.Logger.ForContext<AudioNode<TInit, TPlay>>();
        
        private List<Exception> _exceptions = new List<Exception>();
        
        public TInit InitParams { get; }
        public TPlay PlayParams { get; }
        AudioNodeInitParams IAudioNode.InitParams => InitParams;
        AudioNodePlayParams IAudioNode.PlayParams => PlayParams;
        public AudioLink Output { get; } = new();

        public BufferBlock<AudioDataMessage> InputBufferBlock { get; } = new BufferBlock<AudioDataMessage>(
            new DataflowBlockOptions
            {
                BoundedCapacity = 16
            });

        private IDisposable _inputLink;
        
        public LifecyclePhase Phase { get; set; }
        public readonly LifecycleStateMachine Lifecycle;
        protected AudioNode()
        {
            AudioService.Instance.Graph.AddNode(this);
            Lifecycle = new LifecycleStateMachine(this);
            InitParams = (TInit)Activator.CreateInstance(typeof(TInit));
            PlayParams = (TPlay)Activator.CreateInstance(typeof(TPlay));
        }

        public void ExceptionHappened(Exception exception, string method)
        {
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
                _logger.Error("{e}", exception);
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
            if (InitParams.HasChanged)
            {
                InitParams.Commit();
                Lifecycle.EventHappens(LifecycleEvents.eInit);
            }

            if (PlayParams.Input.HasChanged)
            {
                if (_inputLink != null && PlayParams.Input.LastValue != null)
                {
                    _inputLink.Dispose();
                    _inputLink = null;
                } else if (_inputLink != null || PlayParams.Input.LastValue != null)
                {
                    _logger.Warning("Illegal input link found!");
                    _inputLink?.Dispose();
                    _inputLink = null;
                }

                if (PlayParams.Input.Value != null)
                {
                    _inputLink = PlayParams.Input.Value.SourceBlock.LinkTo(InputBufferBlock);
                }
            }

            if (PlayParams.HasChanged)
            {
                PlayParams.Commit();
                Lifecycle.EventHappens(PlayParams.Playing.Value ? LifecycleEvents.ePlay : LifecycleEvents.eStop);
            }

            if (IsInitValid() && IsPlayValid() && PlayParams.Playing.Value && Phase != LifecyclePhase.Play)
            {
                Lifecycle.EventHappens(PlayParams.Playing.Value ? LifecycleEvents.ePlay : LifecycleEvents.eStop);
            }

            return Output;
        }

        public virtual string DebugInfo()
        {
            return null;
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
            AudioService.Instance.Logger.Information("Dispose called for AudioNode {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    AudioService.Instance.Graph.RemoveNode(this);
                    _inputLink?.Dispose();
                    Output.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}