using System;
using System.Collections.Generic;
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
        public string ErrorMessages();

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
        public readonly LifecycleStateMachine<TConfig> Lifecycle = new LifecycleStateMachine<TConfig>();
        protected AudioNode()
        {
            AudioService.Instance.Graph.AddNode(this);
            Config = (TConfig)Activator.CreateInstance(typeof(TConfig));
        }

        public void ExceptionHappened(Exception exception, string method)
        {
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
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
                else
                {
                    _logger.Error("Cant happen!");
                }
                Config.Input.Commit();
            }
            if (Config.Playing.HasChanged)
            {
                Config.Playing.Commit();
                Lifecycle.EventHappens(Config.Playing.Value ? LifecycleEvents.eStart : LifecycleEvents.eStop, this);
            }
            if (Config.HasChanged)
            {
                Config.Commit();
                Lifecycle.EventHappens(LifecycleEvents.eCreate(Config), this);
            }

            return Output;
        }

        public virtual string DebugInfo()
        {
            return null;
        }

        public string ErrorMessages()
        {
            return _exceptions.Count > 0 ? string.Join(", ", _exceptions) : null;
        }

        public abstract Task<bool> Create(TConfig config);
        public abstract Task<bool> Free();
        public abstract bool Start();
        public abstract bool Stop();

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