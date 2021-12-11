using System;
using VL.NewAudio.Core;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Sources
{
    public abstract class AudioSourceNode : IAudioSource, IDisposable
    {
        private bool _disposedValue;

        protected AudioSourceNode()
        {
        }

        public abstract void PrepareToPlay(int sampleRate, int framesPerBlockExpected);

        public abstract void ReleaseResources();

        public abstract void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill);

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