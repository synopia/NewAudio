using System;
using System.Collections.Generic;

namespace NewAudio.Core
{
    public abstract class BaseNode : IDisposable
    {
        private readonly List<IDisposable> _links = new List<IDisposable>();

        public Action<AudioLink> OnConnect;
        public Action<AudioLink> OnDisconnect;

        private List<Exception> _exceptions = new List<Exception>();

        protected BaseNode()
        {
            AudioService.Instance.Lifecycle.OnPlay += Start;
            AudioService.Instance.Lifecycle.OnStop += Stop;
        }

        public AudioLink Input { get; private set; }

        public AudioLink Output { get; } = new AudioLink();

        public LifecyclePhase Phase => AudioService.Instance.Lifecycle.Phase;

        public virtual void Dispose()
        {
            DisposeLinks();
            if (Input != null) OnDisconnect?.Invoke(Input);

            Output.Dispose();
        }

        protected abstract void Start();
        protected abstract void Stop();

        protected void HandleError(Exception exception)
        {
            if( !_exceptions.Exists(e=>e.Message==exception.Message))
            {
                _exceptions.Add(exception);
                throw exception;
            }
        }

        public void UpdateInput(AudioLink input, bool reset = false)
        {
            if (reset)
                if (Input != null)
                {
                    OnDisconnect?.Invoke(Input);
                    Input = null;
                }

            if (input == Input)
            {
                return;
            }
            
            if (Input == null)
            {
                if (input != null)
                {
                    Input = input;
                    OnConnect?.Invoke(input);
                }
            }
            else
            {
                if (input == null)
                {
                    OnDisconnect?.Invoke(Input);
                    Input = null;
                }
                else
                {
                    OnDisconnect?.Invoke(Input);
                    Input = input;
                    OnConnect?.Invoke(input);
                }
            }
        }

        protected void AddLink(IDisposable disposable)
        {
            _links.Add(disposable);
        }

        protected void DisposeLinks()
        {
            foreach (var link in _links) link.Dispose();
            _links.Clear();
        }
    }
}