using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NAudio.Wave;
using VL.Lib.Collections;

namespace NewAudio
{
    public interface IAudioBufferOwner
    {
        public void Release(AudioBuffer buffer);
    }

    public readonly struct AudioBuffer
    {
        public readonly int Time;
        public readonly IAudioBufferOwner Owner;
        public readonly float[] Data;
        public int Count => Data?.Length ?? 0;

        public AudioBuffer(IAudioBufferOwner owner, int time, float[] data)
        {
            Time = time;
            Owner = owner;
            Data = data;
        }

        public AudioBuffer(IAudioBufferOwner owner, int time, int count) : this(owner, time, new float[count])
        {
        }
    }

    public class AudioBufferFactory : IAudioBufferOwner, IDisposable
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

        public AudioBuffer FromSampleProvider(int time, ISampleProvider provider, int bufferSize)
        {
            AudioBuffer buffer = GetBuffer(time, bufferSize);
            provider.Read(buffer.Data, 0, bufferSize);
            return buffer;
        }

        public AudioBuffer FromByteBuffer(int time, WaveFormat format, byte[] bytes, int bytesRecorded)
        {
            AudioBuffer buffer;
            if (format.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                buffer = GetBuffer(time, bytesRecorded / 4);
                for (int i = 0; i < bytesRecorded / 4; i++)
                {
                    buffer.Data[i] = BitConverter.ToSingle(bytes, i * 4);
                }
            }
            else if (format.BitsPerSample == 32)
            {
                buffer = GetBuffer(time, bytesRecorded / 2);
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

        public AudioBuffer GetBuffer(int time, int length)
        {
            lock (_buffers)
            {
                var result = _buffers.FindIndex(b => b.Data.Length == length);
                if (result >= 0)
                {
                    return _buffers[result];
                }
            }


            return new AudioBuffer(this, time, length);
        }

        public void Dispose()
        {
        }
    }
}