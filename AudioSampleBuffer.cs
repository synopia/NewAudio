using System;
using NAudio.Wave;

namespace VL.NewAudio
{
    public class AudioSampleBuffer : ISampleProvider
    {
        public Action<float[], int, int> Update;

        public AudioSampleBuffer(WaveFormat format)
        {
            AudioEngine.Log($"AudioSampleBuffer: Created {format}");
            WaveFormat = format;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Update?.Invoke(buffer, offset, count);
            return count;
        }

        public static AudioSampleBuffer Silence()
        {
            var buffer = new AudioSampleBuffer(WaveOutput.SingleChannelFormat);
            buffer.Update = (b, o, l) => { Array.Clear(b, o, l); };
            buffer.IsSilence = true;
            return buffer;
        }

        public WaveFormat WaveFormat { get; set; }
        public bool IsSilence;
    }
}