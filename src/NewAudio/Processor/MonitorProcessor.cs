using NewAudio.Dsp;
using NewAudio.Processor;

namespace VL.NewAudio.Processor
{
    public class MonitorProcessor: AudioProcessor
    {
        public int BufferSize { get; set; }

        public RingBuffer[] RingBuffers;
        public override string Name => "Monitor";

        public MonitorProcessor()
        {
            SetChannels(1,0);
            RingBuffers = new RingBuffer[1];
        }

        public override bool IsBusStateSupported(AudioBusState layout)
        {
            return layout.TotalNumberOfOutputChannels == 0;
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
            RingBuffers = new RingBuffer[TotalNumberOfInputChannels];
            for (int i = 0; i < TotalNumberOfInputChannels; i++)
            {
                RingBuffers[i] = new RingBuffer(BufferSize);
            }
        }

        public override void Process(AudioBuffer buffer)
        {
            for (int i = 0; i < TotalNumberOfInputChannels; i++)
            {
                RingBuffers![i].Write(buffer[i].Span, buffer.NumberOfFrames);
            }
        }

        public override void ReleaseResources()
        {
            
        }
    }
}