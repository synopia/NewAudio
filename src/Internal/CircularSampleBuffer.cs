using System;

namespace VL.NewAudio
{
    public class CircularSampleBuffer
    {
        private readonly float[] buffer;
        private readonly object lockObject;
        private int writePosition;
        private int readPosition;
        private int sampleCount;

        /// <summary>Create a new circular buffer</summary>
        /// <param name="size">Max buffer size in bytes</param>
        public CircularSampleBuffer(int size)
        {
            buffer = new float[size];
            lockObject = new object();
        }

        /// <summary>Write data to the buffer</summary>
        /// <param name="data">Data to write</param>
        /// <param name="offset">Offset into data</param>
        /// <param name="count">Number of bytes to write</param>
        /// <returns>number of bytes written</returns>
        public int Write(float[] data, int offset, int count)
        {
            lock (lockObject)
            {
                int num1 = 0;
                if (count > buffer.Length - sampleCount)
                    count = buffer.Length - sampleCount;
                int length = Math.Min(buffer.Length - writePosition, count);
                Array.Copy(data, offset, buffer, writePosition, length);
                writePosition += length;
                writePosition %= buffer.Length;
                int num2 = num1 + length;
                if (num2 < count)
                {
                    Array.Copy(data, offset + num2, buffer, writePosition, count - num2);
                    writePosition += count - num2;
                    num2 = count;
                }

                sampleCount += num2;
                return num2;
            }
        }

        public void Add(float value)
        {
            if (sampleCount < buffer.Length)
            {
                buffer[writePosition] = value;
                writePosition++;
                writePosition %= buffer.Length;
                sampleCount++;
            }
        }

        /// <summary>Read from the buffer</summary>
        /// <param name="data">Buffer to read into</param>
        /// <param name="offset">Offset into read buffer</param>
        /// <param name="count">Bytes to read</param>
        /// <returns>Number of bytes actually read</returns>
        public int Read(float[] data, int offset, int count)
        {
            lock (lockObject)
            {
                if (count > sampleCount)
                    count = sampleCount;
                int num1 = 0;
                int length = Math.Min(buffer.Length - readPosition, count);
                Array.Copy(buffer, readPosition, data, offset, length);
                int num2 = num1 + length;
                readPosition += length;
                readPosition %= buffer.Length;
                if (num2 < count)
                {
                    Array.Copy(buffer, readPosition, data, offset + num2, count - num2);
                    readPosition += count - num2;
                    num2 = count;
                }

                sampleCount -= num2;
                return num2;
            }
        }

        /// <summary>Maximum length of this circular buffer</summary>
        public int MaxLength => buffer.Length;

        /// <summary>
        /// Number of bytes currently stored in the circular buffer
        /// </summary>
        public int Count => sampleCount;

        /// <summary>Resets the buffer</summary>
        public void Reset()
        {
            sampleCount = 0;
            readPosition = 0;
            writePosition = 0;
        }

        /// <summary>Advances the buffer, discarding bytes</summary>
        /// <param name="count">Bytes to advance</param>
        public void Advance(int count)
        {
            if (count >= sampleCount)
            {
                Reset();
            }
            else
            {
                sampleCount -= count;
                readPosition += count;
                readPosition %= MaxLength;
            }
        }
    }
}