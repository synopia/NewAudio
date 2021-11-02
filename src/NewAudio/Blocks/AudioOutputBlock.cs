using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Blocks
{
    public class AudioOutputBlock : ITargetBlock<AudioDataMessage>, IDisposable
    {
        private readonly ILogger _logger;

        private readonly ActionBlock<AudioDataMessage> _actionBlock;
        private readonly CircularBuffer _buffer;
        private bool _firstLoop;

        public AudioOutputBlock(AudioDataflow flow, AudioFormat inputFormat)
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioOutputBlock>();
            AudioService.Instance.Flow.Add(this);
            try
            {
                var name = $"Output Buffer {flow.GetId()}";
                _buffer = new CircularBuffer(name, 32, 4 * inputFormat.BufferSize);
                Buffer = new CircularBuffer(name);

                InputFormat = inputFormat;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }

            AudioService.Instance.Lifecycle.OnPlay += () => { _firstLoop = true; };
            AudioService.Instance.Lifecycle.OnStop += Complete;

            _actionBlock = new ActionBlock<AudioDataMessage>(message =>
            {
                if (_firstLoop)
                {
                    _logger.Information("Audio Output writer started (Writing to {writer} ({owner}))", _buffer.Name,
                        _buffer.IsOwnerOfSharedMemory);
                    _firstLoop = false;
                }

                _logger.Verbose("Writing data to Main Buffer Out {message} {size}", message.Data?.Length,
                    message.BufferSize);

                var pos = 0;
                var token = AudioService.Instance.Lifecycle.GetToken();

                while (pos < message.BufferSize && !token.IsCancellationRequested)
                {
                    var v = _buffer.Write(message.Data, pos, 1);
                    pos += v;
                }

                if (!token.IsCancellationRequested && pos != message.BufferSize) _logger.Warning("pos!=msg {pos}!={msg}", pos, message.BufferSize);
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });
        }

        public CircularBuffer Buffer { get; }

        public AudioFormat InputFormat { get; set; }


        public void Dispose()
        {
            try
            {
                Complete();
                AudioService.Instance.Flow.Remove(this);
                Buffer.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("Dispose: {e}", e);
            }
        }

        public void Complete()
        {
            // _actionBlock.Complete();
        }

        public void Fault(Exception exception)
        {
        }

        public Task Completion => _actionBlock.Completion;

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, AudioDataMessage messageValue,
            ISourceBlock<AudioDataMessage> source, bool consumeToAccept)
        {
            return ((ITargetBlock<AudioDataMessage>)_actionBlock).OfferMessage(messageHeader, messageValue, source,
                consumeToAccept);
        }
    }
}