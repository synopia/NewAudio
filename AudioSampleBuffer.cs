using System;
using NAudio.Wave;

namespace VL.NewAudio
{
    public class AudioSampleBuffer : ISampleProvider
    {
        public Action<float[], int, int> Update;

        public AudioSampleBuffer()
        {
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Update?.Invoke(buffer, offset, count);
            return count;
        }

        public WaveFormat WaveFormat => AudioEngine.InternalFormat;
    }
}