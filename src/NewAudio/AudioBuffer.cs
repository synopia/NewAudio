using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NAudio.Wave;
using VL.Lib.Collections;

namespace NewAudio
{
    public struct AudioBuffer
    {
        public int Time;
        public double DTime;
        public readonly float[] Data;
        public int Count => Data?.Length ?? 0;

        public AudioBuffer(float[] data)
        {
            Data = data;
            Time = 0;
            DTime = 0;
        }

        public AudioBuffer( int count) : this( new float[count])
        {
        }
    }

    public class AudioBufferFactory :  IDisposable
    {
        private readonly List<AudioBuffer> _buffers = new List<AudioBuffer>();

        public int Count
        {
            get
            {
                lock (_buffers)
                {
                    return _buffers.Count;
                }
            }
        }

        public void Clear()
        {
            lock (_buffers)
            {
                _buffers.Clear();
            }
        }

        public AudioBuffer FromSampleProvider( ISampleProvider provider, int bufferSize)
        {
            AudioBuffer buffer = GetBuffer(bufferSize);
            provider.Read(buffer.Data, 0, bufferSize);
            return buffer;
        }

        public AudioBuffer FromByteBuffer( WaveFormat format, byte[] bytes, int bytesRecorded)
        {
            AudioBuffer buffer;
            if (format.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                buffer = GetBuffer(bytesRecorded / 4);
                for (int i = 0; i < bytesRecorded / 4; i++)
                {
                    buffer.Data[i] = BitConverter.ToSingle(bytes, i * 4);
                }
            }
            else if (format.BitsPerSample == 32)
            {
                buffer = GetBuffer(bytesRecorded / 2);
                for (int i = 0; i < bytesRecorded / 2; i++)
                {
                    buffer.Data[i] = BitConverter.ToInt16(bytes, i) / 32768f;
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported format {format}");
            }

            return buffer;
        }

        public void Release(AudioBuffer buffer)
        {
            lock (_buffers)
            {
                // _buffers.Add(buffer);
            }
        }

        public AudioBuffer GetBuffer( int length)
        {
            lock (_buffers)
            {
                var result = _buffers.FindIndex(b => b.Data.Length  == length);
                if (result >= 0)
                {
                    var r = _buffers[result];
                    _buffers.RemoveAt(result);
                    return r;
                }
            }


            return new AudioBuffer(length);
        }

        public void Dispose()
        {
        }
    }
}