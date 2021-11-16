using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace NewAudio.Dsp
{
    public interface IAudioBuffer: IDisposable
    {
        public int NumberOfFrames { get; }
        public int NumberOfChannels { get; }
        public int BytesPerSample { get; }
        public int Size { get; }
        public bool IsEmpty { get; }
        public float[] Data { get; }
        public float this[int key] { get; set; }

        void Zero();
        void Zero(int startFrame, int numFrames);
        void ZeroChannel(int channel);
    }
    public struct AudioBuffer : IAudioBuffer 
    {
        private readonly int _numberOfFrames;
        private readonly int _numberOfChannels;
        private readonly float[] _data;
        public int NumberOfFrames => _numberOfFrames;

        public int NumberOfChannels => _numberOfChannels;

        public int BytesPerSample => 4;
        public int Size => NumberOfChannels * NumberOfFrames;
        public bool IsEmpty => NumberOfFrames == 0;
        public float[] Data => _data;
        private bool _disposed;

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
        
        public AudioBuffer(int numberOfFrames, int numberOfChannels) : this()
        {
            _numberOfFrames = numberOfFrames;
            _numberOfChannels = numberOfChannels;
            _data = ArrayPool<float>.Shared.Rent(numberOfChannels * numberOfFrames);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ArrayPool<float>.Shared.Return(_data);
                _disposed = true;
            }
        }
    }

    public struct AudioBufferInterleaved : IAudioBuffer {
        private readonly int _numberOfFrames;
        private readonly int _numberOfChannels;
        private readonly float[] _data;
        public int NumberOfFrames => _numberOfFrames;

        public int NumberOfChannels => _numberOfChannels;

        public int BytesPerSample => 4;
        public int Size => NumberOfChannels * NumberOfFrames;
        public bool IsEmpty => NumberOfFrames == 0;
        public float[] Data => _data;
        private bool _disposed;

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

        public void Zero(int startFrame, int numFrames)
        {
            new Span<float>(_data, startFrame*NumberOfChannels, numFrames*NumberOfChannels).Fill(0);
        }

        public void ZeroChannel(int channel)
        {
            for (int i = channel; i < NumberOfFrames; i += NumberOfChannels)
            {
                _data[i] = 0;
            }
        }

        public AudioBufferInterleaved(int numberOfFrames, int numberOfChannels) : this()
        {
            _numberOfFrames = numberOfFrames;
            _numberOfChannels = numberOfChannels;
            _data = ArrayPool<float>.Shared.Rent(numberOfChannels * numberOfFrames);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ArrayPool<float>.Shared.Return(_data);
                _disposed = true;
            }
        }
    }

    public struct AudioBufferSpectral : IAudioBuffer
    {
        private readonly int _numberOfFrames;
        private readonly int _numberOfChannels;
        private readonly float[] _data;
        public int NumberOfFrames => _numberOfFrames;

        public int NumberOfChannels => _numberOfChannels;

        public int BytesPerSample => 4;
        public int Size => NumberOfChannels * NumberOfFrames;
        public bool IsEmpty => NumberOfFrames == 0;
        public float[] Data => _data;
        private bool _disposed;

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

        public void Zero(int startFrame, int numFrames)
        {
            new Span<float>(_data, startFrame*NumberOfChannels, numFrames*NumberOfChannels).Fill(0);
        }

        public void ZeroChannel(int channel)
        {
            for (int i = channel; i < NumberOfFrames; i += NumberOfChannels)
            {
                _data[i] = 0;
            }
        }

        public Span<float> GetReal()
        {
            return new Span<float>(_data, 0, NumberOfFrames);
        }
        public Span<float> GetImag()
        {
            return new Span<float>(_data, NumberOfFrames, NumberOfFrames);
        }
        public AudioBufferSpectral(int numberOfFrames): this() 
        {
            _numberOfFrames = numberOfFrames/2;
            _numberOfChannels = 2;
            _data = ArrayPool<float>.Shared.Rent(_numberOfChannels * numberOfFrames);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ArrayPool<float>.Shared.Return(_data);
                _disposed = true;
            }
        }        
    }

    public struct DynamicAudioBuffer : IAudioBuffer
    {
        private int _numberOfFrames;
        private int _numberOfChannels;
        private int _allocatedSize;
        private float[] _data;

        public int NumberOfFrames
        {
            get=> _numberOfFrames;
            set
            {
                _numberOfChannels = value;
                Resize();
            }
        }

        public int NumberOfChannels
        {
            get => _numberOfChannels;
            set
            {
                _numberOfChannels = value;
                Resize();
            }
        }

        public int BytesPerSample => 4;
        public int Size => NumberOfChannels * NumberOfFrames;
        public bool IsEmpty => NumberOfFrames == 0;
        public float[] Data => _data;
        private bool _disposed;

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

        public void Zero(int startFrame, int numFrames)
        {
            new Span<float>(_data, startFrame*NumberOfChannels, numFrames*NumberOfChannels).Fill(0);
        }

        public void ZeroChannel(int channel)
        {
            for (int i = channel; i < NumberOfFrames; i += NumberOfChannels)
            {
                _data[i] = 0;
            }
        }

        public DynamicAudioBuffer(int numberOfFrames=0, int numberOfChannels=1): this() 
        {
            _numberOfFrames = numberOfFrames;
            _numberOfChannels = numberOfChannels;
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
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }        
        
    }
}