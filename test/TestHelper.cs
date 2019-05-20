using System.Collections.Generic;
using VL.NewAudio;

namespace NewAudioTest
{
    public class TestHelper
    {
        public static float[] GenerateBuffer(float[] levels, int len)
        {
            float[] buf = new float[len];
            for (int i = 0; i < len; i++)
            {
                buf[i] = levels[i % levels.Length];
            }

            return buf;
        }

        public static AudioSampleBuffer Silence(float level)
        {
            return new LevelProcessor(level).Build();
        }
    }

    public class LevelProcessor : IAudioProcessor
    {
        private readonly float level;
        public int ReadPos;
        public LevelProcessor(float level)
        {
            this.level = level;
        }

        public List<AudioSampleBuffer> GetInputs()
        {
            return AudioSampleBuffer.EmptyList;
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
            for (int i = 0; i < count; i++)
            {
                buffer[i] = level;
            }

            ReadPos += count;
            return count;
        }
    }
}