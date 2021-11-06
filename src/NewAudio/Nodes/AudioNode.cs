using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private readonly List<IDisposable> _links = new List<IDisposable>();
        private List<Exception> _exceptions = new List<Exception>();
        
        public TConfig Config { get; }
        AudioNodeConfig IAudioNode.Config => Config;
        public AudioLink Output { get; } = new AudioLink();
        public LifecyclePhase Phase { get; set; }
        public readonly LifecycleStateMachine<TConfig> Lifecycle = new LifecycleStateMachine<TConfig>();
        protected AudioNode()
        {
            AudioService.Instance.Graph.AddNode(this);
            Config = (TConfig)Activator.CreateInstance(typeof(TConfig));
            // Lifecycle.EventHappens(LifecycleEvents.eCreate(Config), this).GetAwaiter().GetResult();
            
            Config.Input.OnChange += () =>
            {
                if (Config.Input.LastValue != null)
                {
                    OnDisconnect(Config.Input.Value);
                }
                if (Config.Input.Value != null)
                {
                    OnConnect(Config.Input.Value);
                } 
                return Task.CompletedTask;
            };
            Config.Playing.OnChange += () =>
            {
                return Lifecycle.EventHappens(Config.Playing.Value ? LifecycleEvents.eStart : LifecycleEvents.eStop, this);
            };
            Config.OnChange += OnAnyChange;
            
            // ReSharper disable once VirtualMemberCallInConstructor
            Config.AddGroupOnChange(GetCreateParams(), async () =>
            {
                await Lifecycle.EventHappens(LifecycleEvents.eCreate(Config), this);
            });
        }

        protected abstract IEnumerable<IAudioParam> GetCreateParams();

        public void ExceptionHappened(Exception exception, string method)
        {
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
                _exceptions.Add(exception);
                // throw exception;
            }
        }

        protected async Task<AudioLink> Update()
        {
            // if (BaseConfig.Reset)
            // {
                // if (Config.Input!=null)
                // {
                    // OnDisconnect(Config.Input);
                    // Config.Input = null;
                // }
            // }

            if (Config.HasChanged)
            {
                if (IsInputValid(Config))
                {
                    await Config.Update();
                }
                else
                {
                    Config.Rollback();
                }
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

        public abstract Task<bool> CreateResources(TConfig config);
        public abstract Task<bool> FreeResources();
        public abstract Task<bool> StartProcessing();
        public abstract Task<bool> StopProcessing();

        protected virtual void OnConnect(AudioLink link)
        {
        }

        private void OnDisconnect(AudioLink link)
        {
            DisposeLinks();
        }

        protected virtual bool IsInputValid(TConfig next)
        {
            return true; 
        }

        protected virtual Task OnAnyChange()
        {
            return Task.CompletedTask;
        }

        protected void AddLink(IDisposable disposable)
        {
            _links.Add(disposable);
        }

        protected void DisposeLinks()
        {
            foreach (var link in _links)
            {
                link.Dispose();
            }

            _links.Clear();
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
                    if (Config.Input != null)
                    {
                        OnDisconnect(Config.Input.Value);
                    }
                    DisposeLinks();
                    Output.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}