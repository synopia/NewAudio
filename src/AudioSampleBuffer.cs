using System;
using NAudio.Wave;

namespace VL.NewAudio
{
    public class AudioSampleBuffer : ISampleProvider, IDisposable
    {
        public IAudioProcessor Processor;

        public AudioSampleBuffer(WaveFormat format)
        {
            AudioEngine.Log($"AudioSampleBuffer({GetHashCode()}): Created {format}");
            WaveFormat = format;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (Processor != null)
            {
                return Processor.Read(buffer, offset, count);
            }

            Array.Clear(buffer, offset, count);
            return count;
        }

        public void Dispose()
        {
            AudioEngine.Log($"AudioSampleBuffer({GetHashCode()}): Disposed ");
            Processor = null;
        }

        public static AudioSampleBuffer Silence()
        {
            return new SilenceProcessor().Build();
        }

        public class SilenceProcessor : IAudioProcessor
        {
            public AudioSampleBuffer Build()
            {
                return new AudioSampleBuffer(WaveOutput.SingleChannelFormat)
                {
                    Processor = this
                };
            }

            public int Read(float[] buffer, int offset, int count)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }
        }

        public WaveFormat WaveFormat { get; }
    }
}