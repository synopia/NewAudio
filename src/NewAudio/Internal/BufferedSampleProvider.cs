using System;
using System.Collections.Generic;
using System.Threading;
using NAudio.Wave;
using Serilog;

namespace NewAudio.Internal
{
    public class BufferedSampleProvider : ISampleProvider, IDisposable
    {
        private readonly ILogger _logger = Log.ForContext<BufferedSampleProvider>();
        private CircularSampleBuffer _circularBuffer;
        public float[] Data => _circularBuffer.Data;
        public WaveFormat WaveFormat { get; set; }
        public int WritePos => _circularBuffer.WritePos;
        public int ReadPos => _circularBuffer.ReadPos;
        public int BufferLength
        {
            get=>_circularBuffer?.MaxLength ?? 0;
            set => _circularBuffer = new CircularSampleBuffer(value);
        }

        public int FreeSpace => _circularBuffer?.FreeSpace ?? 0;
        
        public int Overflows;
        public int UnderRuns;

        public int BufferedSamples => _circularBuffer?.Count ?? 0;

        public int AddSamples(float[] buffer, int offset, int count)
        {
            var added = _circularBuffer.Write(buffer, offset, count);
            
            if (added < count)
            {
                Overflows++;
                _logger.Warning(
                    "Added {added}, tried: {count}, overflow: {overflow}, total: {BufferedSamples} len: {BufferLength}", added, count, added<count, BufferedSamples, BufferLength);
            }

            return added;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int num = 0;
            num = _circularBuffer.Read(buffer, offset, count);
            if (num < count)
            {
                Array.Clear(buffer, offset + num, count - num);
                UnderRuns++;
                _logger.Warning(
                    "Read {num}, tried: {count}, underrun: {underrun} total: {BufferedSamples} len: {BufferLength}", num, count, num<count, BufferedSamples, BufferLength);
                // num = count;
            }

            return count;
        }

        public void ClearBuffer()
        {
            _circularBuffer.Reset();
        }

        public void Dispose()
        {
            _circularBuffer?.Reset();
            _circularBuffer = null;
        }
    }
}