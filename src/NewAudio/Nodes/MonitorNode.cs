using System;
using VL.NewAudio.Nodes;
using VL.Lang;
using VL.Lib.Collections;
using VL.NewAudio.Processor;

namespace VL.NewAudio.Nodes
{
    public class MonitorNode: AudioProcessorNode<MonitorProcessor>
    {
        private int _bufferSize;
        public int BufferSize
        {
            get=>_bufferSize;
            set
            {
                _bufferSize = value;
                Processor.BufferSize = BufferSize*2;
                _data = new float[BufferSize];
            }
        }

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
    }
}