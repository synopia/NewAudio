using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VL.NewAudio.Dsp
{
    public struct AudioChannel
    {
        private float[] _buffer;
        private int _offset;
        private int _numFrames;

        public float[] Buffer => _buffer;
        public int TotalOffset => _offset;
        public int NumFrames => _numFrames;

        public AudioChannel(float[] buffer, int offset, int numFrames)
        {
            _buffer = buffer;
            _offset = offset;
            _numFrames = numFrames;
        }

        public AudioChannel Offset(int offset)
        {
            return new AudioChannel(_buffer, _offset + offset, _numFrames-offset);
        }

        public float this[int index]
        {
            get => _buffer[_offset + index];
            set => _buffer[_offset + index] = value;
        }

        public Span<float> AsSpan()
        {
            return _buffer.AsSpan(_offset, _numFrames);
        }
        public Span<float> AsSpan(int numFrames)
        {
            return _buffer.AsSpan(_offset, numFrames);
        }
        public void Zero()
        {
            AsSpan().Clear();
        }
        public void Zero(int start, int numFrames)
        {
            _buffer.AsSpan(_offset+start, numFrames).Clear();
        }

        public int CopyTo(AudioChannel other, int numFrames=-1)
        {
            numFrames = Math.Min(numFrames == -1 ? _numFrames : numFrames, other._numFrames);
            AsSpan(numFrames).CopyTo(other.AsSpan(numFrames));
            return numFrames;
        }

        public void Add(float value, int numFrames=-1)
        {
            VectorOp.Add.Scalar(this, value, numFrames == -1 ? _numFrames : numFrames);
        }

        public void Add(AudioChannel other, int numFrames=-1)
        {
            VectorOp.Add.Op(this, other, numFrames == -1 ? _numFrames : numFrames);
        }

        public void Mul(float value, int numFrames=-1)
        {
            VectorOp.Mul.Scalar(this, value, numFrames == -1 ? _numFrames : numFrames);
        }
        public void Mul(AudioChannel other, int numFrames=-1)
        {
            VectorOp.Mul.Op(this, other, numFrames == -1 ? _numFrames : numFrames);
        }
        
    }
}