using System;
using System.Diagnostics;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.Lib.Mathematics;

namespace VL.NewAudio.Processor
{
    
    public abstract class GenProcessor: AudioProcessor
    {
        public float Freq { get; set; }
        protected float _period;
        protected float _phase;
        
        protected GenProcessor()
        {
            SetChannels(0, 1);
        }
        
        public override bool IsBusStateSupported(AudioBusState layout)
        {
            return layout.MainBusInputChannels == 0 && layout.MainBusOutputChannels == 1;
        }

        protected abstract float NextSample();
        
        public override void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
            _period = 1.0f / SampleRate;
        }
        public override void Process(AudioBuffer buffer)
        {
            Trace.Assert(buffer.NumberOfChannels<=TotalNumberOfOutputChannels);
            
            for (int i = 0; i < buffer.NumberOfFrames; i++)
            {
                var sample = NextSample();
                for (int ch = 0; ch < TotalNumberOfOutputChannels; ch++)
                {
                    buffer[ch, i] = sample;
                }
            }
        }

        public override void ReleaseResources()
        {
            
        }
    }

    public class NoiseGenProcessor : GenProcessor
    {
        public override string Name => "Noise";
        private Random _random = new Random();
        
        public NoiseGenProcessor()
        {
        }

        protected override float NextSample()
        {
            return _random.NextFloat(-1, 1);
        }
    }
    public class SineGenProcessor : GenProcessor
    {
        public override string Name => "SineGen";
        
        public SineGenProcessor()
        {
        }

        protected override float NextSample()
        {
            var sample = AudioMath.SinF(_phase * AudioMath.TwoPi);
            var increase = Freq * _period;
            _phase = AudioMath.Fract(_phase + increase);
            return sample;
        }
    }
}