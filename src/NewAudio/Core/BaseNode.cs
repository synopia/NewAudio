using System;
using System.Collections.Generic;
using NewAudio.Blocks;
using Serilog;
using VL.NewAudio.Core;

namespace NewAudio.Core
{
    public abstract class BaseNode : IDisposable 
    {
        private readonly AudioLink _output = new AudioLink();
        protected AudioDataflow Flow;
        public AudioLink Input { get; private set; }

        public Action<AudioLink> Connect;
        public Action<AudioLink> Disconnect;
        public Action<AudioLink, AudioLink> Reconnect;

        public AudioLink Output => _output;
        public LifecyclePhase Phase => AudioService.Instance.Lifecycle.Phase;

        private List<IDisposable> _links = new List<IDisposable>();

        protected BaseNode()
        {
            Flow = AudioService.Instance.Flow;
            AudioService.Instance.Lifecycle.OnPlay += Start;
            AudioService.Instance.Lifecycle.OnStop += Stop;
        }

        protected abstract void Start();
        protected abstract void Stop();
        
        
        public void UpdateInput(AudioLink input, bool reset=false)
        {
            if (reset)
            {
                if (Input != null)
                {
                    Disconnect?.Invoke(Input);
                    Input = null;
                }
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
            foreach (var link in _links)
            {
                link.Dispose();
            }
            _links.Clear();
        }
        
        public virtual void Dispose()
        {
            DisposeLinks();
            if (Input != null)
            {
                Disconnect?.Invoke(Input);
            }

            _output.Dispose();
        }
    }
    
    
}