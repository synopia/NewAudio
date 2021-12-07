using System;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Device
{
    public abstract class AudioSourceNode : IAudioSource, IDisposable
    {
        private bool _disposedValue;
        public AudioConnection Output { get; }
        
        private AudioConnection? _input;
        public AudioConnection? Input
        {
            get => _input;
            set
            {
                _input = value;
            }
        }

        protected AudioSourceNode()
        {
            Output = new(this);
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
                }

                _disposedValue = true;
            }
        }

    }
}