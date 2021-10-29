using System;

namespace NewAudio
{
    public interface IAudioProcessor
    {
        public float ProcessSample(float input);
    }

    public class AudioLevelProcessor : IAudioProcessor
    {
        public int Level { get; set; }


        public float ProcessSample(float input)
        {
            return Math.Max(-1.0f, Math.Min(1.0f, input*Level));
        }
    }
}