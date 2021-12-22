using System;
using VL.NewAudio.Core;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Sources
{
    public abstract class AudioSourceBase : IAudioSource, IDisposable
    {
        private bool _disposedValue;

        protected AudioSourceBase()
        {
        }

        public abstract void PrepareToPlay(int sampleRate, int framesPerBlockExpected);

        public abstract void ReleaseResources();

        public abstract void FillNextBuffer(AudioBufferToFill buffer);

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
                    ReleaseResources();
                }

                _disposedValue = true;
            }
        }
    }
}