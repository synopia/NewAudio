using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace VL.NewAudio
{
    public class AudioSampleBuffer : ISampleProvider, IDisposable
    {
        public static List<AudioSampleBuffer> EmptyList = new List<AudioSampleBuffer>();

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
            public List<AudioSampleBuffer> GetInputs()
            {
                return EmptyList;
            }

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