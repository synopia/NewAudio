using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Nodes
{
    public class RingBuffer: BaseNode
    {
        private readonly ILogger _logger;
        private RingBufferBlock _ringBufferBlock;
        private AudioFormat _format;
        private readonly BufferBlock<AudioDataMessage> _bufferBlock;

        public int NodeCount { get; private set; }
        public string Name { get; private set; }
        
        public RingBuffer()
        {
            _format = new AudioFormat(48000, 512, 2);

            _logger = AudioService.Instance.Logger.ForContext<RingBuffer>();

            _bufferBlock = new BufferBlock<AudioDataMessage>(new DataflowBlockOptions()
            {
                BoundedCapacity = 16
            });
            
            OnConnect += link =>
            {
                Output.SourceBlock = _ringBufferBlock;
                Output.Format = link.Format;
                
                AddLink(link.SourceBlock.LinkTo(_bufferBlock));
            };

            OnDisconnect += link =>
            {
                DisposeLinks();
            };

        }

        public void ChangeSettings(AudioLink input, int nodeCount = 16, string name = null)
        {
            NodeCount = Math.Max(2, nodeCount);
            Name = name;
            
            _ringBufferBlock?.Dispose();
            _ringBufferBlock = new RingBufferBlock(AudioService.Instance.Flow, link.Format, NodeCount, Name);                

            UpdateInput(input);
        }
        
        protected override bool IsInputValid(AudioLink link)
        {
            return true;
        }

        protected override void Start()
        {
        }

        protected override void Stop()
        {
        }
    }
}