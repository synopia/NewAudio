using System;
using System.Buffers;
using VL.Lib.Collections;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;

namespace VL.NewAudio.Sources
{
    public abstract class BaseBufferOutSource : AudioSourceBase
    {
        private int _sampleRate;
        private int _framesPerBlock;
        private int _bufferSize;
        private RingBuffer? _ringBuffer;
        private float[]? _data;

        private IAudioSource? _source;

        public IAudioSource? Source
        {
            get => _source;
            set
            {
                if (_source != value)
                {
                    var oldSource = _source;

                    if (value != null && _framesPerBlock > 0 && _sampleRate > 0)
                    {
                        value.PrepareToPlay(_sampleRate, _framesPerBlock);
                    }

                    _source = value;

                    oldSource?.ReleaseResources();
                }
            }
        }

        public bool Overflow { get; set; }

        public int BufferSize
        {
            get => _bufferSize;
            set
            {
                if (_bufferSize == value)
                {
                    return;
                }

                _bufferSize = value;
                Resize();
            }
        }

        protected abstract void OnDataReady(float[] data);

        public void FillBuffer()
        {
            if (_ringBuffer == null || _data == null)
            {
                return;
            }

            if (_ringBuffer.AvailableRead >= BufferSize)
            {
                _ringBuffer.Read(_data, BufferSize);
                OnDataReady(_data);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_data != null)
            {
                ArrayPool<float>.Shared.Return(_data);
            }

            base.Dispose(disposing);
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            _sampleRate = sampleRate;
            _framesPerBlock = framesPerBlockExpected;
            _source?.PrepareToPlay(sampleRate, framesPerBlockExpected);
        }

        public override void ReleaseResources()
        {
            _source?.ReleaseResources();
        }

        private void Resize()
        {
            if (_data != null)
            {
                ArrayPool<float>.Shared.Return(_data);
            }

            _data = ArrayPool<float>.Shared.Rent(_bufferSize);
            _ringBuffer = new RingBuffer(_bufferSize * 2);
        }

        public override void FillNextBuffer(AudioBufferToFill buffer)
        {
            using var s = new ScopedMeasure("BaseBufferOutSource.GetNextAudioBlock");
            if (_source != null)
            {
                _source.FillNextBuffer(buffer);
            }
            // else
            // {
            // bufferToFill.ClearActiveBuffer();
            // }

            if (buffer.Buffer.NumberOfChannels > 0 && _ringBuffer != null)
            {
                Overflow = _ringBuffer
                    .Write(buffer.Buffer[0].Offset(buffer.StartFrame).AsSpan(buffer.NumFrames),
                        buffer.NumFrames);
            }
        }
    }
}