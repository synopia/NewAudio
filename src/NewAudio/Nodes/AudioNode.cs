using System;
using System.Collections.Generic;
using NewAudio.Core;

namespace NewAudio.Nodes
{
    public interface IAudioNodeConfig
    {
        AudioLink Input { get; set; }
        LifecyclePhase Phase { get; set; }
        
        bool Reset { get; set; }
        bool HasChanged { get; set; }
    }

    public interface IAudioNode : IDisposable
    {
        public IAudioNodeConfig Config { get; }
        public string DebugInfo();
        public string ErrorMessages();

    }
    public abstract class AudioNode<TConfig> : IAudioNode where TConfig: IAudioNodeConfig
    {
        private readonly List<IDisposable> _links = new List<IDisposable>();
        private List<Exception> _exceptions = new List<Exception>();
        
        public AudioParams AudioParams = new AudioParams(typeof(TConfig));
        public TConfig Config { get; }
        IAudioNodeConfig IAudioNode.Config => Config;
        public TConfig LastConfig { get; }
        public TConfig NextConfig { get; }
        public AudioLink Output { get; } = new AudioLink();

        protected AudioNode()
        {
            AudioService.Instance.Graph.AddNode(this);
            Config = AudioParams.Create<TConfig>();
            LastConfig = AudioParams.CreateLast<TConfig>();
            NextConfig = AudioParams.CreateNext<TConfig>();
            
            AudioParams.Get<AudioLink>("Input").OnCommit += (last, current) =>
            {
                if (last != null)
                {
                    OnDisconnect(last);
                }
                if (current != null)
                {
                    OnConnect(current);
                } 
            };
            AudioParams.Get<LifecyclePhase>("Phase").OnCommit += (last, current) =>
            {
                if (current == LifecyclePhase.Playing)
                {
                    OnStart();
                }

                if (current == LifecyclePhase.Stopped)
                {
                    OnStop();
                }
            };
            AudioParams.OnCommit += OnAnyChange;
        }

        public void RegisterCallback<T>(string name, Action<T, T> action)
        {
            AudioParams.Get<T>(name).OnCommit += action;
        }
        

        public void HandleError(Exception exception)
        {
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
                _exceptions.Add(exception);
                throw exception;
            }
        }

        protected AudioLink Update()
        {
            // if (BaseConfig.Reset)
            // {
                // if (Config.Input!=null)
                // {
                    // OnDisconnect(Config.Input);
                    // Config.Input = null;
                // }
            // }

            if (AudioParams.HasChanged)
            {
                if (IsInputValid(NextConfig))
                {
                    AudioParams.Commit();
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

        protected virtual void OnStart()
        {
        }
        protected virtual void OnStop()
        {
        }
        protected virtual void OnConnect(AudioLink link)
        {
        }
        protected virtual void OnDisconnect(AudioLink link)
        {
            DisposeLinks();

        }

        protected virtual bool IsInputValid(TConfig next)
        {
            return true; 
        }

        protected virtual void OnAnyChange()
        {
            
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
        
        public virtual void Dispose()
        {
            DisposeLinks();
            if (Config.Input != null)
            {
                OnDisconnect(Config.Input);
            }

            Output.Dispose();
        }
        
    }
}