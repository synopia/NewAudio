using System;
using Serilog;
using VL.NewAudio.Core;

namespace VL.NewAudio.Nodes
{
    public abstract class AudioNode : IDisposable
    {
        protected ILogger Logger = Resources.GetLogger<AudioNode>();
        public IAudioService AudioService { get; }

        public virtual bool IsEnable { get; set; }
        public virtual bool IsEnabled { get; } = false;
        private bool _disposedValue;

        protected AudioNode()
        {
            Logger.Information("Created audio node {@This}", this);
            AudioService = Resources.GetAudioService();
        }

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