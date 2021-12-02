using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using NewAudio.Core;
using NewAudio.Device;
using NewAudio.Processor;
using VL.Core.Diagnostics;

namespace NewAudio.Nodes
{
    public abstract class AudioNode: IDisposable
    {
        public IAudioService AudioService { get; }
        
        public virtual bool Enable { get; set; }
        public abstract bool Enabled { get; }
        public List<string> Messages = new List<string>();
        protected AudioNode()
        {
            AudioService = Resources.GetAudioService();
        }

        
        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = true;
            }
        }

    }
}