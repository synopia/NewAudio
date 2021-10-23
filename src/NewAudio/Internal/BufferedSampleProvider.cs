using System;
using System.Collections.Generic;
using System.Threading;
using NAudio.Wave;

namespace NewAudio.Internal
{
    public class BufferedSampleProvider : ISampleProvider, IDisposable
    {
        private readonly Logger _logger = LogFactory.Instance.Create("CircularBuffer");
        private CircularSampleBuffer _circularBuffer;
        public float[] Data => _circularBuffer.Data;

        public int BufferLength
        {
            get=>_circularBuffer?.MaxLength ?? 0;
            set => _circularBuffer = new CircularSampleBuffer(value);
        }

        public int FreeSpace => _circularBuffer?.FreeSpace ?? 0;
        
        public TimeSpan BufferDuration
        {
            get { return TimeSpan.FromSeconds(BufferLength / (double)WaveFormat.AverageBytesPerSecond * 4); }
            set { BufferLength = (int)(value.TotalSeconds * WaveFormat.AverageBytesPerSecond / 4); }
        }

        public bool IsValid => WaveFormat != null && BufferLength > 0;
        public int Overflows;
        public int UnderRuns;

        public int BufferedSamples => _circularBuffer?.Count ?? 0;

        public TimeSpan BufferedDuration =>
            TimeSpan.FromSeconds(BufferedSamples / (double)WaveFormat.AverageBytesPerSecond * 4);

        public void Advance(TimeSpan timeSpan)
        {
            _circularBuffer?.Advance((int)(timeSpan.TotalSeconds * WaveFormat.AverageBytesPerSecond / 4));
        }

        public void Advance(int samples)
        {
            _circularBuffer?.Advance(samples);
        }

        public WaveFormat WaveFormat { get; set; }
        public void AddSamples(float[] buffer, int offset, int count)
        {
            var added = _circularBuffer.Write(buffer, offset, count);
            if (added < count)
            {
                Overflows++;
                _logger.Info(
                    $"Added {added}, tried: {count}, overflow: {added < count}, total: {BufferedSamples} len: {BufferLength}");
            }

        }

        public int Read(float[] buffer, int offset, int count)
        {
            int num = 0;
            num = _circularBuffer.Read(buffer, offset, count);
            if (num < count)
            {
                Array.Clear(buffer, offset + num, count - num);
                UnderRuns++;
                // num = count;
                _logger.Info(
                    $"Read {num}, tried: {count}, underrun: {num<count} total: {BufferedSamples} len: {BufferLength}");
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