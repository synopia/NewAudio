using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Blocks
{
    public class AudioOutputBlock : ITargetBlock<AudioDataMessage>, IDisposable
    {
        private readonly ILogger _logger;

        public CircularBuffer Buffer { get; }

        public AudioFormat InputFormat { get; set; }

        private ActionBlock<AudioDataMessage> _actionBlock;

        public AudioOutputBlock(AudioDataflow flow, AudioFormat inputFormat)
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioInputBlock>();
            AudioService.Instance.Flow.Add(this);
            try
            {
                Buffer = new CircularBuffer($"Output Buffer {flow.GetId()}", 64, inputFormat.BufferSize);
                InputFormat = inputFormat;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}",e);
            }

            _actionBlock = new ActionBlock<AudioDataMessage>(message =>
            {
                _logger.Verbose("Writing data to Main Buffer Out {message} {size}",message.Data.Length, message.BufferSize);

                var pos = 0;
                var token = AudioService.Instance.Lifecycle.GetToken();
                while (pos<message.BufferSize && !token.IsCancellationRequested)
                {
                    var v = Buffer.Write(message.Data, pos);
                    pos += v;
                }
            });
            
            
        }

        public void Dispose()
        {
            try
            {
                AudioService.Instance.Flow.Remove(this);
                Buffer.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("Dispose: {e}",e);
            }
        }

        public void Complete()
        {
            _actionBlock.Complete();
        }

        public void Fault(Exception exception)
        {
        }

        public Task Completion => _actionBlock.Completion;

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, AudioDataMessage messageValue,
            ISourceBlock<AudioDataMessage> source, bool consumeToAccept)
        {
            return ((ITargetBlock<AudioDataMessage>)_actionBlock).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }
    }
}