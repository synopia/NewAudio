using System;
using System.Buffers;
using VL.Lib.Collections;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;

namespace VL.NewAudio.Sources
{
    public abstract class BaseBufferOutSource : AudioSourceNode
    {
        private readonly object _readLock = new();
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

                    lock (_readLock)
                    {
                        _source = value;
                    }

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

                lock (_readLock)
                {
                    _bufferSize = value;
                    Resize();
                }
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
                lock (_readLock)
                {
                    _ringBuffer.Read(_data, BufferSize);
                    OnDataReady(_data);
                }
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
            lock (_readLock)
            {
                _sampleRate = sampleRate;
                _framesPerBlock = framesPerBlockExpected;
                _source?.PrepareToPlay(sampleRate, framesPerBlockExpected);
            }
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

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            using var s = new ScopedMeasure("BaseBufferOutSource.GetNextAudioBlock");
            lock (_readLock)
            {
                if (_source != null)
                {
                    _source.GetNextAudioBlock(bufferToFill);
                }
                // else
                // {
                // bufferToFill.ClearActiveBuffer();
                // }

                if (bufferToFill.Buffer.NumberOfChannels > 0 && _ringBuffer != null)
                {
                    Overflow = _ringBuffer
                        .Write(bufferToFill.Buffer[0].Offset(bufferToFill.StartFrame).AsSpan(bufferToFill.NumFrames),
                            bufferToFill.NumFrames);
                }
            }
        }
    }
}