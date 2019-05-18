using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace VL.NewAudio
{
    public class BufferedSampleProvider : ISampleProvider, IAudioProcessor
    {
        private CircularSampleBuffer circularBuffer;
        public int BufferLength { get; set; }

        public TimeSpan BufferDuration
        {
            get { return TimeSpan.FromSeconds(BufferLength / (double) WaveFormat.AverageBytesPerSecond * 4); }
            set { BufferLength = (int) (value.TotalSeconds * WaveFormat.AverageBytesPerSecond / 4); }
        }

        public bool IsValid => WaveFormat != null && BufferLength > 0;
        public int Overflows;
        public int UnderRuns;

        public int BufferedSamples
        {
            get
            {
                if (circularBuffer != null)
                    return circularBuffer.Count;
                return 0;
            }
        }

        public TimeSpan BufferedDuration =>
            TimeSpan.FromSeconds(BufferedSamples / (double) WaveFormat.AverageBytesPerSecond * 4);

        public List<AudioSampleBuffer> GetInputs()
        {
            return AudioSampleBuffer.EmptyList;
        }

        public void Advance(TimeSpan timeSpan)
        {
            circularBuffer?.Advance((int) (timeSpan.TotalSeconds * WaveFormat.AverageBytesPerSecond / 4));
        }

        public WaveFormat WaveFormat { get; set; }

        public void AddSamples(float[] buffer, int offset, int count)
        {
            if (circularBuffer == null)
                circularBuffer = new CircularSampleBuffer(BufferLength);
            if (circularBuffer.Write(buffer, offset, count) < count)
                Overflows++;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int num = 0;
            if (circularBuffer != null)
                num = circularBuffer.Read(buffer, offset, count);
            if (num < count)
            {
                Array.Clear(buffer, offset + num, count - num);
                UnderRuns++;
            }

            return count;
        }

        public void ClearBuffer()
        {
            circularBuffer?.Reset();
            circularBuffer = null;
        }
    }
}