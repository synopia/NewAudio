using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace NewAudio.Dsp
{
    public abstract class BaseAudioBuffer : IDisposable
    {
        protected int _numberOfFrames;
        protected int _numberOfChannels;
        protected float[] _data;

        public virtual int NumberOfFrames
        {
            get => _numberOfFrames;
            set => throw new Exception();
        }

        public virtual int NumberOfChannels
        {
            get => _numberOfChannels;
            set => throw new Exception();
        }

        public int BytesPerSample => 4;
        public int Size => NumberOfChannels * NumberOfFrames;
        public bool IsEmpty => NumberOfFrames == 0;
        public float[] Data => _data;

        private bool _disposedValue;

        public float this[int key]
        {
            get => _data[key];
            set => _data[key] = value;
        }

        public void Zero()
        {
            var s = new Span<float>(_data);
            s.Fill(0f);
        }

        protected BaseAudioBuffer(int numberOfFrames, int numberOfChannels)
        {
            _numberOfFrames = numberOfFrames;
            _numberOfChannels = numberOfChannels;
            _data = ArrayPool<float>.Shared.Rent(numberOfChannels * numberOfFrames);
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
                    ArrayPool<float>.Shared.Return(_data);
                    _data = null;
                }

                _disposedValue = true;
            }
        }
    }

    public class AudioBuffer : BaseAudioBuffer
    {
        public AudioBuffer(int numberOfFrames = 0, int numberOfChannels = 1) : base(numberOfFrames, numberOfChannels)
        {
        }

        public Span<float> GetChannel(int channel)
        {
            return new Span<float>(_data, channel * NumberOfFrames, NumberOfFrames);
        }

        public void Zero(int startFrame, int numFrames)
        {
            for (int ch = 0; ch < NumberOfChannels; ch++)
            {
                GetChannel(ch).Slice(startFrame, numFrames).Fill(0);
            }
        }

        public void ZeroChannel(int channel)
        {
            GetChannel(channel).Fill(0);
        }

        public void CopyTo(AudioBuffer other)
        {
            var numFrames = System.Math.Min(NumberOfFrames, other.NumberOfFrames);
            CopyTo(other, numFrames);
        }

        public void CopyTo(AudioBuffer other, int numFrames)
        {
            var numChannels = System.Math.Min(NumberOfChannels, other.NumberOfChannels);
            for (int ch = 0; ch < numChannels; ch++)
            {
                GetChannel(ch).Slice(0, numFrames).CopyTo(other.GetChannel(ch));
            }
        }

        public void CopyOffset(AudioBuffer other, int numFrames, int frameOffset, int otherFrameOffset)
        {
            for (int ch = 0; ch < NumberOfChannels; ch++)
            {
                GetChannel(ch).Slice(frameOffset, numFrames).CopyTo(other.GetChannel(ch).Slice(otherFrameOffset));
            }
        }

        public void CopyChannel(AudioBuffer other, int channel, int otherChannel)
        {
            GetChannel(channel).CopyTo(other.GetChannel(otherChannel));
        }
    }

    public class AudioBufferInterleaved : BaseAudioBuffer
    {
        public void Zero(int startFrame, int numFrames)
        {
            new Span<float>(_data, startFrame * NumberOfChannels, numFrames * NumberOfChannels).Fill(0);
        }

        public void ZeroChannel(int channel)
        {
            for (int i = channel; i < NumberOfFrames; i += NumberOfChannels)
            {
                _data[i] = 0;
            }
        }

        public AudioBufferInterleaved(int numberOfFrames = 0, int numberOfChannels = 1) : base(numberOfFrames,
            numberOfChannels)
        {
        }
    }

    public class AudioBufferSpectral : AudioBuffer
    {
        public AudioBufferSpectral(int numberOfFrames = 0) : base(numberOfFrames / 2, 2)
        {
        }

        public Span<float> GetReal()
        {
            return new Span<float>(_data, 0, NumberOfFrames);
        }

        public Span<float> GetImag()
        {
            return new Span<float>(_data, NumberOfFrames, NumberOfFrames);
        }
    }

    public class DynamicAudioBuffer : AudioBuffer
    {
        private int _allocatedSize;

        public override int NumberOfFrames
        {
            get => _numberOfFrames;
            set
            {
                _numberOfFrames = value;
                Resize();
            }
        }

        public override int NumberOfChannels
        {
            get => _numberOfChannels;
            set
            {
                _numberOfChannels = value;
                Resize();
            }
        }

        public void SetSize(int numberOfFrames, int numberOfChannels)
        {
            _numberOfFrames = numberOfFrames;
            _numberOfChannels = numberOfChannels;
            Resize();
        }
        
        public DynamicAudioBuffer(int numberOfFrames = 0, int numberOfChannels = 1) : base(numberOfFrames, numberOfChannels)
        {
            Resize();
        }

        public void Shrink()
        {
            if (_allocatedSize > Size)
            {
                var newData = ArrayPool<float>.Shared.Rent(Size);
                if (_allocatedSize > 0)
                {
                    Array.Copy(_data, newData, Size);
                    ArrayPool<float>.Shared.Return(_data);
                }

                _allocatedSize = Size;
                _data = newData;
            }
        }

        private void Resize()
        {
            if (_allocatedSize < Size)
            {
                var newData = ArrayPool<float>.Shared.Rent(Size);
                if (_allocatedSize > 0)
                {
                    Array.Copy(_data, newData, _allocatedSize);
                    ArrayPool<float>.Shared.Return(_data);
                }

                _allocatedSize = Size;
                _data = newData;
            }
        }
    }
}