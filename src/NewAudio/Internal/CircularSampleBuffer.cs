using System;
using NAudio.Wave;
using Serilog;

namespace NewAudio.Internal
{
    public class CircularSampleBuffer
    {
        private readonly ILogger _logger = Log.ForContext<CircularSampleBuffer>();
        private readonly float[] _buffer;
        public float[] Data => _buffer;
        private readonly object _lockObject;
        private int _writePosition;
        private int _readPosition;
        private int _sampleCount;

        public int WritePos => _writePosition;
        public int ReadPos => _readPosition;

        public Action<float[]> BufferFilled;
        
        /// <summary>Create a new circular buffer</summary>
        /// <param name="size">Max buffer size in samples</param>
        public CircularSampleBuffer(int size)
        {
            _buffer = new float[size];
            _lockObject = new object();
        }
        
        
        /// <summary>Write data to the buffer</summary>
        /// <param name="data">Data to write</param>
        /// <param name="offset">Offset into data</param>
        /// <param name="count">Number of samples to write</param>
        /// <returns>number of samples written</returns>
        public int Write(float[] data, int offset, int count)
        {
            lock (_lockObject)
            {
                int num1 = 0;
                if (count > _buffer.Length - _sampleCount)
                    count = _buffer.Length - _sampleCount;
                int length = Math.Min(_buffer.Length - _writePosition, count);
                Array.Copy(data, offset, _buffer, _writePosition, length);
                _writePosition += length;

                if (_writePosition>=_buffer.Length && BufferFilled != null)
                {
                    BufferFilled(_buffer);
                }

                _writePosition %= _buffer.Length;
                int num2 = num1 + length;
                if (num2 < count)
                {
                    Array.Copy(data, offset + num2, _buffer, _writePosition, count - num2);
                    _writePosition += count - num2;
                    num2 = count;
                }

                _sampleCount += num2;
                return num2;
            }
        }

        public void Add(float value)
        {
            if (_sampleCount < _buffer.Length)
            {
                _buffer[_writePosition] = value;
                _writePosition++;
                _writePosition %= _buffer.Length;
                if (_writePosition == 0 && BufferFilled!=null )
                {
                    BufferFilled(_buffer);
                }
                _sampleCount++;
            }
        }

        /// <summary>Read from the buffer</summary>
        /// <param name="data">Buffer to read into</param>
        /// <param name="offset">Offset into read buffer</param>
        /// <param name="count">samples to read</param>
        /// <returns>Number of samples actually read</returns>
        public int Read(float[] data, int offset, int count)
        {
            lock (_lockObject)
            {
                if (count > _sampleCount)
                    count = _sampleCount;
                int num1 = 0;
                int length = Math.Min(_buffer.Length - _readPosition, count);
                Array.Copy(_buffer, _readPosition, data, offset, length);
                int num2 = num1 + length;
                _readPosition += length;
                _readPosition %= _buffer.Length;
                if (num2 < count)
                {
                    Array.Copy(_buffer, _readPosition, data, offset + num2, count - num2);
                    _readPosition += count - num2;
                    num2 = count;
                }

                _sampleCount -= num2;
                return num2;
            }
        }

        /// <summary>Maximum length of this circular buffer</summary>
        public int MaxLength => _buffer.Length;

        /// <summary>
        /// Number of samples currently stored in the circular buffer
        /// </summary>
        public int Count => _sampleCount;

        public int FreeSpace => MaxLength - Count;

        /// <summary>Resets the buffer</summary>
        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _sampleCount = 0;
            _readPosition = 0;
            _writePosition = 0;
        }

        /// <summary>Advances the buffer, discarding samples</summary>
        /// <param name="count">samples to advance</param>
        public void Advance(int count)
        {
            if (count >= _sampleCount)
            {
                Reset();
            }
            else
            {
                _sampleCount -= count;
                _readPosition += count;
                _readPosition %= MaxLength;
            }
        }
    }
}