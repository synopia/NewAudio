﻿using System;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Sources
{
    public class MemoryAudioSource : AudioSourceNode, IPositionalAudioSource
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

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            if (_buffer.NumberOfFrames == 0)
            {
                bufferToFill.ClearActiveBuffer();
                return;
            }

            var channels = Math.Min(bufferToFill.Buffer.NumberOfChannels, _buffer.NumberOfChannels);
            int max, pos = 0;
            var n = _buffer.NumberOfFrames;
            var m = bufferToFill.NumFrames;
            var i = _position;
            for (; (i < n || IsLooping) && pos < m; i += max)
            {
                max = Math.Min(m - pos, n - i % n);
                var ch = 0;
                for (; ch < channels; ch++)
                {
                    _buffer[ch].Slice(i % n, max).CopyTo(bufferToFill.Buffer[ch].Slice(bufferToFill.StartFrame + pos));
                }

                for (; ch < bufferToFill.Buffer.NumberOfChannels; ch++)
                {
                    bufferToFill.Buffer[ch].Slice(bufferToFill.StartFrame + pos, max).Span.Clear();
                }

                pos += max;
            }

            if (pos < m)
            {
                bufferToFill.Buffer.Zero(bufferToFill.StartFrame + pos, m - pos);
            }

            _position = i;
        }

        public ulong NextReadPos
        {
            get => (ulong)_position;

            set => _position = (int)value;
        }

        public ulong TotalLength => (ulong)_buffer.NumberOfFrames;
        public bool IsLooping { get; set; }
    }
}