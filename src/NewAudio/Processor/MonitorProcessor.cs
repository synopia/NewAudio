using System;
using VL.NewAudio.Dsp;
using VL.NewAudio.Processor;

namespace VL.NewAudio.Processor
{
    public class MonitorProcessor : AudioProcessor
    {
        public int BufferSize { get; set; }

        public RingBuffer[] RingBuffers;
        public override string Name => "Monitor";

        public MonitorProcessor()
        {
            SetChannels(1, 0);
            RingBuffers = Array.Empty<RingBuffer>();
        }

        public override bool IsBusStateSupported(AudioBusState layout)
        {
            return layout.TotalNumberOfOutputChannels == 0;
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
            RingBuffers = new RingBuffer[TotalNumberOfInputChannels];
            for (var i = 0; i < TotalNumberOfInputChannels; i++)
            {
                RingBuffers[i] = new RingBuffer(BufferSize);
            }
        }

        public override void Process(AudioBuffer buffer)
        {
            for (var i = 0; i < TotalNumberOfInputChannels; i++)
            {
                RingBuffers[i].Write(buffer[i].AsSpan(), buffer.NumberOfFrames);
            }
        }

        public override void ReleaseResources()
        {
        }
    }
}