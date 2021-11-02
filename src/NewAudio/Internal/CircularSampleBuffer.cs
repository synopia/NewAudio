using System;
using Serilog;

namespace NewAudio.Internal
{
    public class CircularSampleBuffer
    {
        private readonly object _lockObject;
        private readonly ILogger _logger = Log.ForContext<CircularSampleBuffer>();

        public Action<float[]> BufferFilled;

        /// <summary>Create a new circular buffer</summary>
        /// <param name="size">Max buffer size in samples</param>
        public CircularSampleBuffer(int size)
        {
            Data = new float[size];
            _lockObject = new object();
        }

        public float[] Data { get; }

        public int WritePos { get; private set; }

        public int ReadPos { get; private set; }

        /// <summary>Maximum length of this circular buffer</summary>
        public int MaxLength => Data.Length;

        /// <summary>
        ///     Number of samples currently stored in the circular buffer
        /// </summary>
        public int Count { get; private set; }

        public int FreeSpace => MaxLength - Count;


        /// <summary>Write data to the buffer</summary>
        /// <param name="data">Data to write</param>
        /// <param name="offset">Offset into data</param>
        /// <param name="count">Number of samples to write</param>
        /// <returns>number of samples written</returns>
        public int Write(float[] data, int offset, int count)
        {
            lock (_lockObject)
            {
                var num1 = 0;
                if (count > Data.Length - Count)
                    count = Data.Length - Count;
                var length = Math.Min(Data.Length - WritePos, count);
                Array.Copy(data, offset, Data, WritePos, length);
                WritePos += length;

                if (WritePos >= Data.Length && BufferFilled != null) BufferFilled(Data);

                WritePos %= Data.Length;
                var num2 = num1 + length;
                if (num2 < count)
                {
                    Array.Copy(data, offset + num2, Data, WritePos, count - num2);
                    WritePos += count - num2;
                    num2 = count;
                }

                if (WritePos >= Data.Length && BufferFilled != null) BufferFilled(Data);

                Count += num2;
                return num2;
            }
        }

        public void Add(float value)
        {
            if (Count < Data.Length)
            {
                Data[WritePos] = value;
                WritePos++;
                WritePos %= Data.Length;
                if (WritePos == 0 && BufferFilled != null) BufferFilled(Data);
                Count++;
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
                if (count > Count)
                    count = Count;
                var num1 = 0;
                var length = Math.Min(Data.Length - ReadPos, count);
                Array.Copy(Data, ReadPos, data, offset, length);
                var num2 = num1 + length;
                ReadPos += length;
                ReadPos %= Data.Length;
                if (num2 < count)
                {
                    Array.Copy(Data, ReadPos, data, offset + num2, count - num2);
                    ReadPos += count - num2;
                    num2 = count;
                }

                Count -= num2;
                return num2;
            }
        }

        /// <summary>Resets the buffer</summary>
        public void Reset()
        {
            Array.Clear(Data, 0, Data.Length);
            Count = 0;
            ReadPos = 0;
            WritePos = 0;
        }

        /// <summary>Advances the buffer, discarding samples</summary>
        /// <param name="count">samples to advance</param>
        public void Advance(int count)
        {
            if (count > Count)
            {
                Reset();
            }
            else
            {
                Count -= count;
                ReadPos += count;
                ReadPos %= MaxLength;
            }
        }
    }
}