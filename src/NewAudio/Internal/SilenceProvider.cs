using System;
using NAudio.Wave;

namespace NewAudio.Internal
{
    public class SilenceProvider: ISampleProvider
    {
        public SilenceProvider()
        {
            // WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(AudioCore.Instance.Settings.SampleRate, 1);
            // AudioCore.Instance.Settings.ObsSampleRate.Subscribe(sr =>
            // {
                // WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat((int)sr, 1);
            // });
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        public WaveFormat WaveFormat { get; private set; }
    }
}