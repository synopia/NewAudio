using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Nodes
{
    public class AudioNodeConfig: AudioParams
    {
        public AudioParam<bool> Playing;
        public AudioParam<AudioLink> Input;
    }

    public interface IAudioNode : IDisposable, ILifecycleDevice
    {
        public AudioNodeConfig Config { get; }
        public string DebugInfo();
        public IEnumerable<string> ErrorMessages();

    }
    public abstract class AudioNode<TConfig>: IAudioNode, ILifecycleDevice<TConfig, bool> where TConfig: AudioNodeConfig
    {
        private readonly ILogger _logger = AudioService.Instance.Logger.ForContext<AudioNode<TConfig>>();
        
        private List<Exception> _exceptions = new List<Exception>();
        
        public TConfig Config { get; }
        AudioNodeConfig IAudioNode.Config => Config;
        public AudioLink Output { get; } = new AudioLink();

        public BufferBlock<AudioDataMessage> InputBufferBlock { get; } = new BufferBlock<AudioDataMessage>(
            new DataflowBlockOptions
            {
                BoundedCapacity = 16
            });

        private IDisposable _inputLink;
        
        public LifecyclePhase Phase { get; set; }
        public readonly LifecycleStateMachine<TConfig> Lifecycle;
        protected AudioNode()
        {
            AudioService.Instance.Graph.AddNode(this);
            Lifecycle = new LifecycleStateMachine<TConfig>(this);
            Config = (TConfig)Activator.CreateInstance(typeof(TConfig));
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

            if (Config.Input.HasChanged)
            {
                if (_inputLink != null && Config.Input.LastValue != null)
                {
                    _inputLink.Dispose();
                    _inputLink = null;
                } else if (_inputLink != null || Config.Input.LastValue != null)
                {
                    _logger.Warning("Illegal input link found!");
                    _inputLink?.Dispose();
                    _inputLink = null;
                }

                if (Config.Input.Value != null)
                {
                    _inputLink = Config.Input.Value.SourceBlock.LinkTo(InputBufferBlock);
                }
            }

            var sendPlaying = false;
            if (Config.Playing.HasChanged)
            {
                Config.Playing.Commit();
                sendPlaying = true;
            }
            if (Config.HasChanged)
            {
                Config.Commit();
                Lifecycle.EventHappens(LifecycleEvents.eCreate(Config));
            }

            if (sendPlaying)
            {
                Lifecycle.EventHappens(Config.Playing.Value ? LifecycleEvents.eStart : LifecycleEvents.eStop);
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

        public abstract Task<bool> Create(TConfig config);
        public abstract Task<bool> Free();
        public abstract bool Start();
        public abstract Task<bool> Stop();

        public virtual bool IsInputValid(TConfig next)
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
                    _inputLink?.Dispose();
                    Output.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}