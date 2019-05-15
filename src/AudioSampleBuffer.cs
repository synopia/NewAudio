using System;
using NAudio.Wave;

namespace VL.NewAudio
{
    public class AudioSampleBuffer : ISampleProvider
    {
        public Func<float[], int, int, int> Update;
        private bool isSilence;

        public AudioSampleBuffer(WaveFormat format)
        {
            AudioEngine.Log($"AudioSampleBuffer: Created {format}");
            WaveFormat = format;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return Update?.Invoke(buffer, offset, count) ?? count;
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