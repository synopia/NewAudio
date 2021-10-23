using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NAudio.Wave;
using VL.Lib.Collections;

namespace  NewAudio
{
    public interface IAudioBufferOwner
    {
        public void Release(AudioBuffer buffer);
    }
    public readonly struct AudioBuffer
    {
        public readonly IAudioBufferOwner Owner;
        public readonly float[] Data;
        public readonly int Size; 

        public AudioBuffer(IAudioBufferOwner owner, float[] data, int size)
        {
            Size = size;
            Owner = owner;
            Data = data;
        }
        public AudioBuffer(IAudioBufferOwner owner, int size):this(owner, new float[size], size)
        {
        }

        public Spread<float> GetSampleData(Spread<int> channels)
        {
            return Spread<float>.Empty;
        }
    }

    public class AudioBufferFactory : IAudioBufferOwner, IDisposable
    {
        private readonly List<AudioBuffer> _buffers = new List<AudioBuffer>();

        public void Clear()
        {
            lock (_buffers)
            {
                _buffers.Clear();
            }
        }
        public AudioBuffer FromByteBuffer(WaveFormat format, byte[] bytes, int bytesRecorded)
        {
            AudioBuffer buffer;
            if (format.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                buffer = GetBuffer(bytesRecorded / 4);
                for (int i = 0; i < bytesRecorded / 4; i++)
                {
                    buffer.Data[i] = BitConverter.ToSingle(bytes, i*4);
                }
                
            }else if (format.BitsPerSample == 32)
            {
                buffer = GetBuffer(bytesRecorded / 4);
                for (int i = 0; i < bytesRecorded / 4; i++)
                {
                    buffer.Data[i] = BitConverter.ToInt32(bytes, i*4) / (float)int.MaxValue;
                }
            } 
            else if (format.BitsPerSample == 16)
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
                _buffers.Add(buffer);
            }
        }

        private AudioBuffer GetBuffer(int length)
        {
            lock (_buffers)
            {
                var result = _buffers.FindIndex(b => b.Data.Length == length);
                if (result>=0)
                {
                    return _buffers[result];
                }                
            }
            

            return new AudioBuffer(this, length);
        }

        public void Dispose()
        {
            
        }
    }
}