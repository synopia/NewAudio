using System;
using NAudio.Wave;

namespace VL.NewAudio
{
    public class AudioSampleBuffer : ISampleProvider, IDisposable
    {
        public Func<float[], int, int, int> Update;
        private bool isSilence;

        public AudioSampleBuffer(WaveFormat format)
        {
            AudioEngine.Log($"AudioSampleBuffer({GetHashCode()}): Created {format}");
            WaveFormat = format;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (Update != null)
            {
                return Update.Invoke(buffer, offset, count);
            }

            Array.Clear(buffer, offset, count);
            return count;
        }

        public void Dispose()
        {
            AudioEngine.Log($"AudioSampleBuffer({GetHashCode()}): Disposed ");
            Update = null;
        }

        public static AudioSampleBuffer Silence()
        {
            var buffer = new AudioSampleBuffer(WaveOutput.SingleChannelFormat)
            {
                Update = (b, o, c) =>
                {
                    Array.Clear(b, o, c);
                    return c;
                },
                isSilence = true
            };
            return buffer;
        }

        public WaveFormat WaveFormat { get; }

        public bool IsSilence => isSilence;
    }
}