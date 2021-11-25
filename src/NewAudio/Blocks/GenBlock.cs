﻿using System;
using NewAudio.Core;
using NewAudio.Dsp;
using VL.Lib.Mathematics;

namespace NewAudio.Block
{
    public class GenBlockParams : AudioParams
    {
        public AudioParam<float> Freq;
    }
    
    public abstract class GenBlock: InputBlock
    {
        public readonly GenBlockParams Params;
        protected float _period;
        protected float _phase;
        
        protected GenBlock(AudioBlockFormat format) : base(format)
        {
            InitLogger<GenBlock>();
            Params = AudioParams.Create<GenBlockParams>();
            
            ChannelMode = ChannelMode.Specified;
            NumberOfChannels = 1;
        }

        protected override void Initialize()
        {
            _period = 1.0f / SampleRate;
        }
    }

    public class NoiseGenBlock : GenBlock
    {
        public override string Name => "Noise";
        private Random _random = new Random();
        
        public NoiseGenBlock(AudioBlockFormat format) : base(format)
        {
        }

        protected override void Process(AudioBuffer buffer, int numFrames)
        {
            var data = buffer.Data.AsSpan(FrameProcessRange.Item1, FrameProcessRange.Item2);
            for (var i = 0; i < numFrames; i++)
            {
                data[i] = _random.NextFloat(-1f, 1f);
            }
        }
    }
    public class SineGenBlock : GenBlock
    {
        public override string Name => "SineGen";
        
        public SineGenBlock(AudioBlockFormat format) : base(format)
        {
        }

        protected override void Process(AudioBuffer buffer, int numFrames)
        {
            var data = buffer.Data.AsSpan(FrameProcessRange.Item1, FrameProcessRange.Item2);
            var incr = Params.Freq.Value * _period;
            for (var i = 0; i < numFrames; i++)
            {
                data[i] = AudioMath.SinF(_phase * AudioMath.TwoPi);
                _phase = AudioMath.Fract(_phase + incr);
            }
        }
    }
}