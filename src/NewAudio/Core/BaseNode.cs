using System;
using System.Collections.Generic;
using NewAudio.Blocks;
using Serilog;

namespace NewAudio.Core
{
    public abstract class BaseNode<TBaseBlock> : IDisposable where TBaseBlock: BaseAudioBlock
    {
        protected readonly ILogger Logger;
        public LifecyclePhase Phase => AudioBlock.CurrentPhase;
        private readonly AudioLink _output = new AudioLink();
        public AudioLink Input { get; private set; }

        public Action<AudioLink> Connect;
        public Action<AudioLink> Disconnect;
        public Action<AudioLink, AudioLink> Reconnect;

        public AudioLink Output => _output;
        public abstract TBaseBlock AudioBlock { get; }
        private List<IDisposable> _links = new List<IDisposable>();
        protected readonly AudioGraph Graph;

        protected BaseNode()
        {
            Logger = AudioService.Instance.Logger;
            Graph = AudioService.Instance.Graph;
        }
        
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
            Logger.Information("Disposing {this}", this);
            DisposeLinks();
            if (Input != null)
            {
                Disconnect?.Invoke(Input);
            }

            _output.Dispose();
        }
    }
    
    
}