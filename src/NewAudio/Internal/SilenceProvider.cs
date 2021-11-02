using System;
using NAudio.Wave;

namespace NewAudio.Internal
{
    public class SilenceProvider : ISampleProvider
    {
        public SilenceProvider(WaveFormat format)
        {
            WaveFormat = format;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        public WaveFormat WaveFormat { get; }
    }
}