using System;
using System.Collections.Generic;

namespace NewAudio.Core
{
    public abstract class BaseNode : IDisposable
    {
        private readonly List<IDisposable> _links = new List<IDisposable>();

        public Action<AudioLink> Connect;
        public Action<AudioLink> Disconnect;
        protected AudioDataflow Flow;
        public Action<AudioLink, AudioLink> Reconnect;

        protected BaseNode()
        {
            Flow = AudioService.Instance.Flow;
            AudioService.Instance.Lifecycle.OnPlay += Start;
            AudioService.Instance.Lifecycle.OnStop += Stop;
        }

        public AudioLink Input { get; private set; }

        public AudioLink Output { get; } = new AudioLink();

        public LifecyclePhase Phase => AudioService.Instance.Lifecycle.Phase;

        public virtual void Dispose()
        {
            DisposeLinks();
            if (Input != null) Disconnect?.Invoke(Input);

            Output.Dispose();
        }

        protected abstract void Start();
        protected abstract void Stop();


        public void UpdateInput(AudioLink input, bool reset = false)
        {
            if (reset)
                if (Input != null)
                {
                    Disconnect?.Invoke(Input);
                    Input = null;
                }

            if (Input == null)
            {
                if (input != null)
                {
                    Input = input;
                    Connect?.Invoke(input);
                }
            }
            else
            {
                if (input == null)
                {
                    Disconnect?.Invoke(Input);
                    Input = null;
                }
                else
                {
                    var old = Input;
                    Input = input;
                    Reconnect?.Invoke(old, input);
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