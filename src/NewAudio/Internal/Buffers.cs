using System;
using NAudio.Wave;
using SharedMemory;

namespace NewAudio.Core
{
    public static class Buffers
    {
        /*
        public static unsafe void ReadAll(CircularBuffer buffer, float[] data, int bytes, CancellationToken token)
        {
            fixed (float* b = data)
            {
                IntPtr ptr = new IntPtr(b);
                var pos = 0;
                bytes *= 4;
                while (pos < bytes && !token.IsCancellationRequested)
                {
                    var read = buffer.Read(ptr, bytes-pos);
                    pos += read;
                    ptr += read;
                }

                if (pos != bytes)
                {
                    AudioService.Instance.Logger.Error("ReadAll: {pos} != {bytes}, t={token}", pos, bytes,
                        token.IsCancellationRequested);
                }
            }
        }

        public static unsafe void WriteAll(CircularBuffer buffer, float[] data, int messageSize, CancellationToken token)
        {
            fixed (float* b = data)
            {
                IntPtr ptr = new IntPtr(b);

                var pos = 0;
                messageSize *= 4;
                while (pos < messageSize && !token.IsCancellationRequested)
                {
                    var written = buffer.Write(ptr, messageSize-pos);
                    ptr += written;
                    pos += written;
                }

                if (pos != messageSize)
                {
                    AudioService.Instance.Logger.Error("WriteAll(f): {pos} != {bytes}, t={token}", pos, messageSize,
                        token.IsCancellationRequested);
                }
            }
        }
        public static unsafe void ReadAll(CircularBuffer buffer, byte[] data, int bytes, CancellationToken token)
        {
            fixed (byte* b = data)
            {
                IntPtr ptr = new IntPtr(b);
                var pos = 0;
                while (pos < bytes && !token.IsCancellationRequested)
                {
                    var read = buffer.Read(ptr, bytes-pos);
                    pos += read;
                    ptr += read;
                }

                if (pos != bytes)
                {
                    AudioService.Instance.Logger.Error("ReadAll: {pos} != {bytes}, t={token}", pos, bytes,
                        token.IsCancellationRequested);
                }
            }
        }
        */

        public static unsafe int Write(CircularBuffer buffer, byte[] data, int offset, int bytes, int timeout)
        {
            fixed (byte* buf = data)
            {
                var ptr = new IntPtr(buf) + offset;
                return buffer.Write(ptr, bytes, timeout);
            }
        }
        /*
        public static unsafe void WriteAll(CircularBuffer buffer, byte[] data, int offset, int bytes, CancellationToken token)
        {
            fixed (byte* buf = data)
            {
                IntPtr ptr = new IntPtr(buf)+offset;
                var pos = 0;

                while (pos < bytes && !token.IsCancellationRequested)
                {
                    var written = buffer.Write(ptr, bytes-pos);
                    ptr += written;
                    pos += written;
                }

                if (pos != bytes)
                {
                    AudioService.Instance.Logger.Error("WriteAll: {pos} != {bytes}, t={token}", pos, bytes,
                        token.IsCancellationRequested);
                }
            }
        }
        */

        public static void FromSampleProvider(float[] buffer, ISampleProvider provider, int bufferSize)
        {
            provider.Read(buffer, 0, bufferSize);
        }

        public static void FromByteBuffer(float[] buffer, WaveFormat format, byte[] bytes, int bytesRecorded)
        {
            if (format.Encoding == WaveFormatEncoding.IeeeFloat)
                Buffer.BlockCopy(bytes, 0, buffer, 0, bytesRecorded);
            /*
            else if (format.BitsPerSample == 32)
            {
                buffer = GetBuffer(bytesRecorded / 2);
                for (int i = 0; i < bytesRecorded / 2; i++)
                {
                    buffer.Data[i] = BitConverter.ToInt16(bytes, i) / 32768f;
                }
            }
            */
            else
                throw new ArgumentException($"Unsupported format {format}");
        }
    }
}