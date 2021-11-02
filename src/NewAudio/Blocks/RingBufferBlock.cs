using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Blocks
{
    public class RingBufferBlock : BaseAudioBlock
    {
        private readonly ILogger _logger;
        private readonly CircularBuffer _buffer;
        public CircularBuffer Buffer { get; }
        public AudioFormat OutputFormat { get; set; }

        public RingBufferBlock(AudioDataflow flow, AudioFormat outputFormat, int nodeCount, string name=null) : base(flow)
        {
            _logger = AudioService.Instance.Logger.ForContext<RingBufferBlock>();
            _logger.Information("Ring buffer block created");
            try
            {
                name ??= $"RingBuffer {flow.GetId()}";
                _buffer = new CircularBuffer(name, nodeCount, 4 * outputFormat.BufferSize);
                Buffer = new CircularBuffer(name);

                OutputFormat = outputFormat;

                Target = new ActionBlock<AudioDataMessage>((input) =>
                {
                    
                });
                Source = new BufferBlock<AudioDataMessage>();
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }
        }

        public override void Dispose()
        {
            try
            {
                _buffer.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("Dispose: {e}", e);
            }
            base.Dispose();
        }
    }
}