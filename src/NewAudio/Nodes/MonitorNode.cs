using System;
using NewAudio.Nodes;
using VL.Lang;
using VL.Lib.Collections;
using VL.NewAudio.Processor;

namespace VL.NewAudio.Nodes
{
    public class MonitorNode: AudioProcessorNode<MonitorProcessor>
    {
        public int BufferSize { get; set; }
        private float[] _data = Array.Empty<float>();
        
        public Spread<float> Buffer { get; set; } = Spread<float>.Empty;

        public MonitorNode() : base(new MonitorProcessor())
        {
            
        }

        public void FillBuffer()
        {
            if (Processor.RingBuffers.Length <= 0)
            {
                return;
            }

            var ringBuffer = Processor.RingBuffers[0];
            if (ringBuffer.AvailableRead >= BufferSize)
            {
                ringBuffer.Read(_data, BufferSize);
                Buffer = Spread.Create(_data);
            }
        }

        public override Message? Update(ulong mask)
        {
            if (HasChanged(nameof(BufferSize), mask))
            {
                Processor.BufferSize = BufferSize*2;
                _data = new float[BufferSize];
            }

            return null;
        }
    }
}