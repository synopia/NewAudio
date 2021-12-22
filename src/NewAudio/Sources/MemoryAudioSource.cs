using System;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Sources
{
    public class MemoryAudioSource : AudioSourceBase, IPositionalAudioSource
    {
        private readonly AudioBuffer _buffer;
        private int _position;


        public MemoryAudioSource(AudioBuffer buffer)
        {
            _buffer = buffer;
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            _position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            _buffer.Dispose();
            base.Dispose(disposing);
        }

        public override void ReleaseResources()
        {
        }

        public override void FillNextBuffer(AudioBufferToFill buffer)
        {
            if (_buffer.NumberOfFrames == 0)
            {
                buffer.ClearActiveBuffer();
                return;
            }

            var channels = Math.Min(buffer.Buffer.NumberOfChannels, _buffer.NumberOfChannels);
            int max, pos = 0;
            var n = _buffer.NumberOfFrames;
            var m = buffer.NumFrames;
            var i = _position;
            for (; (i < n || IsLooping) && pos < m; i += max)
            {
                max = Math.Min(m - pos, n - i % n);
                var ch = 0;
                for (; ch < channels; ch++)
                {
                    _buffer[ch].Offset(i % n).CopyTo(buffer.Buffer[ch].Offset(buffer.StartFrame + pos), max);
                }

                for (; ch < buffer.Buffer.NumberOfChannels; ch++)
                {
                    buffer.Buffer[ch].Zero(buffer.StartFrame + pos, max);
                }

                pos += max;
            }

            if (pos < m)
            {
                buffer.Buffer.Zero(buffer.StartFrame + pos, m - pos);
            }

            _position = i;
        }

        public long NextReadPos
        {
            get => _position;

            set => _position = (int)value;
        }

        public long TotalLength => _buffer.NumberOfFrames;
        public bool IsLooping { get; set; }
    }
}