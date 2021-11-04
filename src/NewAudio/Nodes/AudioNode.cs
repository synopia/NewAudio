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
        AudioParams AudioParams { get; }
        AudioLink Output { get; }
        void OnAnyChange();
        void OnConnect(AudioLink input);
        void OnDisconnect(AudioLink link);

        void OnStart();
        void OnStop();
        
    }
    public interface IAudioNode<TConfig> : IAudioNode where TConfig : IAudioNodeConfig
    {
         TConfig Config { get; }
         TConfig LastConfig { get; }

        bool IsInputValid(TConfig next);

    }
    public class AudioNodeSupport<TConfig> : IDisposable where TConfig: IAudioNodeConfig
    {
        private readonly List<IDisposable> _links = new List<IDisposable>();
        private List<Exception> _exceptions = new List<Exception>();
        
        public AudioParams AudioParams = new AudioParams(typeof(TConfig));
        public TConfig Config { get; }
        public TConfig LastConfig { get; }
        public TConfig NextConfig { get; }
        public AudioLink Output { get; } = new AudioLink();
        private IAudioNode<TConfig> _node;

        public AudioNodeSupport(IAudioNode<TConfig> node)
        {
            _node = node;
            AudioService.Instance.Graph.AddNode(node);
            Config = AudioParams.Create<TConfig>();
            LastConfig = AudioParams.CreateLast<TConfig>();
            NextConfig = AudioParams.CreateNext<TConfig>();
            
            AudioParams.Get<AudioLink>("Input").OnCommit += (last, current) =>
            {
                if (last != null)
                {
                    node.OnDisconnect(last);
                }
                if (current != null)
                {
                    node.OnConnect(current);
                } 
            };
            AudioParams.Get<LifecyclePhase>("Phase").OnCommit += (last, current) =>
            {
                if (current == LifecyclePhase.Playing)
                {
                    node.OnStart();
                }

                if (current == LifecyclePhase.Stopped)
                {
                    node.OnStop();
                }
            };
            AudioParams.OnCommit += node.OnAnyChange;
        }

        public void RegisterCallback<T>(string name, Action<T, T> action)
        {
            AudioParams.Get<T>(name).OnCommit += action;
        }
        
        public void Dispose()
        {
            DisposeLinks();
            if (Config.Input != null)
            {
                _node.OnDisconnect(Config.Input);
            }

            Output.Dispose();
            AudioService.Instance.Graph.RemoveNode((IAudioNode<IAudioNodeConfig>)_node);
        }

        public void HandleError(Exception exception)
        {
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
                _exceptions.Add(exception);
                throw exception;
            }
        }

        public void Update(/*AudioLink input, LifecyclePhase phase, bool reset = false*/)
        {
            if (Config.Reset)
            {
                // if (Config.Input!=null)
                // {
                    // OnDisconnect(Config.Input);
                    // Config.Input = null;
                // }
            }

            if (AudioParams.HasChanged)
            {
                if (_node.IsInputValid(NextConfig))
                {
                    AudioParams.Commit();
                }
            }
        }

        public void AddLink(IDisposable disposable)
        {
            _links.Add(disposable);
        }

        public void DisposeLinks()
        {
            foreach (var link in _links)
            {
                link.Dispose();
            }

            _links.Clear();
        }
    }
}