﻿using System;

namespace NewAudio.Dsp
{
    public static class MixBuffers
    {
        public static void MixBuffer(AudioBuffer source, AudioBuffer target)
        {
            int sourceChannels = source.NumberOfChannels;
            int targetChannels = target.NumberOfChannels;
            var minFrames = Math.Min(source.NumberOfFrames, target.NumberOfFrames);
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
                var channel = target.GetChannel(0);
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    Dsp.AddMul(channel, source.GetChannel(0), downMix, channel, minFrames);
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
        
        public static void SumMixBuffer(AudioBuffer source, AudioBuffer target)
        {
            int sourceChannels = source.NumberOfChannels;
            int targetChannels = target.NumberOfChannels;
            var minFrames = Math.Min(source.NumberOfFrames, target.NumberOfFrames);
            if (targetChannels == sourceChannels)
            {
                Dsp.Add(source.Data, target.Data, target.Data, minFrames);
            } else if (sourceChannels == 1)
            {
                for (int ch = 0; ch < targetChannels; ch++)
                {
                    Dsp.Add(source.GetChannel(0), target.GetChannel(ch),target.GetChannel(ch),minFrames);
                }
            } else if (targetChannels == 1)
            {
                float downMix = 1.0f / (float)Math.Sqrt(2.0);
                var channel = target.GetChannel(0);
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    Dsp.AddMul(channel, source.GetChannel(0), downMix, channel, minFrames);
                }
            } else if (targetChannels < sourceChannels)
            {
                for (int ch = 0; ch < targetChannels; ch++)
                {
                    Dsp.Add(source.GetChannel(ch), target.GetChannel(ch),target.GetChannel(ch),minFrames);
                }
            } else if (targetChannels > sourceChannels)
            {
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    Dsp.Add(source.GetChannel(ch), target.GetChannel(ch),target.GetChannel(ch),minFrames);
                }
            }
        }
        
        
    }
}