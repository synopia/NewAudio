using System;

namespace NewAudio.Dsp
{
    public static class MixBuffers
    {
        public static void MixBuffer(AudioBuffer source, AudioBuffer target, int numFrames)
        {
            int sourceChannels = source.NumberOfChannels;
            int targetChannels = target.NumberOfChannels;
            if (targetChannels == sourceChannels)
            {
                source.CopyTo(target);
            } else if (sourceChannels == 1)
            {
                for (int ch = 0; ch < targetChannels; ch++)
                {
                    source.CopyChannel(target, 0, ch);
                }
            } else if (targetChannels == 1)
            {
                float downMix = 1.0f / (float)Math.Sqrt(2.0);
                var channel = target.GetReadChannel(0);
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    Dsp.AddMul(channel, source.GetReadChannel(0), downMix, channel, numFrames);
                }
            } else if (targetChannels < sourceChannels)
            {
                for (int ch = 0; ch < targetChannels; ch++)
                {
                    source.CopyChannel(target, ch, ch);
                }
            } else if (targetChannels > sourceChannels)
            {
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    source.CopyChannel(target, ch, ch);
                }
            }
        }
        
        public static void SumMixBuffer(AudioBuffer source, AudioBuffer target, int numFrames)
        {
            int sourceChannels = source.NumberOfChannels;
            int targetChannels = target.NumberOfChannels;
            if (targetChannels == sourceChannels)
            {
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    Dsp.Add(source.GetReadChannel(ch), target.GetReadChannel(ch), target.GetReadChannel(ch), numFrames);
                    
                }                
            } else if (sourceChannels == 1)
            {
                for (int ch = 0; ch < targetChannels; ch++)
                {
                    Dsp.Add(source.GetReadChannel(0), target.GetReadChannel(ch),target.GetReadChannel(ch),numFrames);
                }
            } else if (targetChannels == 1)
            {
                float downMix = 1.0f / (float)Math.Sqrt(2.0);
                var channel = target.GetReadChannel(0);
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    Dsp.AddMul(channel, source.GetReadChannel(0), downMix, channel, numFrames);
                }
            } else if (targetChannels < sourceChannels)
            {
                for (int ch = 0; ch < targetChannels; ch++)
                {
                    Dsp.Add(source.GetReadChannel(ch), target.GetReadChannel(ch),target.GetReadChannel(ch),numFrames);
                }
            } else if (targetChannels > sourceChannels)
            {
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    Dsp.Add(source.GetReadChannel(ch), target.GetReadChannel(ch),target.GetReadChannel(ch),numFrames);
                }
            }
        }
        
        
    }
}