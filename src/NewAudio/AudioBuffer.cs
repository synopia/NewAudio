using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace NewAudio
{
    public readonly struct AudioTime
    {
        public readonly int Time;
        public readonly double DTime;

        public AudioTime(int time, double dTime)
        {
            Time = time;
            DTime = dTime;
        }

        public static AudioTime operator +(AudioTime a, AudioTime b) =>
            new AudioTime(a.Time + b.Time, a.DTime + b.DTime);
        public static AudioTime operator +(AudioTime a, AudioFormat f) =>
            new AudioTime(a.Time + f.SampleCount, a.DTime + f.SampleCount/(double)f.SampleRate);

        public static bool operator ==(AudioTime a, AudioTime b) => a.Time == b.Time;
        public static bool operator !=(AudioTime a, AudioTime b) => a.Time != b.Time;

        public override string ToString()
        {
            return $"[{Time}, {DTime}]";
        }
    }
    public struct AudioBuffer
    {
        public AudioTime Time;
        public readonly float[] Data;
        public int Count => Data?.Length ?? 0;

        public AudioBuffer(float[] data)
        {
            Data = data;
            Time = default;
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