using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Blocks
{
    public class AudioOutputBlock : ITargetBlock<AudioDataMessage>, IAudioBlock
    {
        private readonly ILogger _logger;
        private readonly BufferBlock<AudioDataMessage> _bufferBlock = new BufferBlock<AudioDataMessage>(
            new DataflowBlockOptions
            {
                BoundedCapacity = 100,
                MaxMessagesPerTask = 4
            });

        private IDisposable _link;
        private ActionBlock<AudioDataMessage> _actionBlock;
        private readonly CircularBuffer _buffer;
        private CancellationTokenSource _cancellationTokenSource;

        public CircularBuffer Buffer { get; }

        public AudioFormat InputFormat { get; set; }

        public AudioOutputBlock(AudioFormat inputFormat)
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioOutputBlock>();
            try
            {
                var name = $"Output Buffer {AudioService.Instance.Graph.GetNextId()}";
                _buffer = new CircularBuffer(name, 32, 4 * inputFormat.BufferSize);
                Buffer = new CircularBuffer(name);

                InputFormat = inputFormat;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }

        }

        public void Play()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            var firstLoop = true;
            if (_link != null)
            {
                _logger.Warning("Link != null! {link}", _link);
            }
            if (_actionBlock != null)
            {
                _logger.Warning("ActionBlock != null! {link}", _link);
            }
            _actionBlock = new ActionBlock<AudioDataMessage>(message =>
            {
                if (firstLoop)
                {
                    _logger.Information("Audio Output writer started (Writing to {writer} ({owner}))", _buffer.Name,
                        _buffer.IsOwnerOfSharedMemory);
                    firstLoop = false;
                }

                _logger.Verbose("Writing data to Main Buffer Out {message} {size}", message.Data?.Length,
                    message.BufferSize);

                var pos = 0;

                while (pos < message.BufferSize && !token.IsCancellationRequested)
                {
                    var v = _buffer.Write(message.Data, pos, 1);
                    pos += v;
                }

                if (!token.IsCancellationRequested && pos != message.BufferSize)
                {
                    _logger.Warning("pos!=msg {pos}!={msg}", pos, message.BufferSize);
                }
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                CancellationToken = token
            });
            _link = _bufferBlock.LinkTo(_actionBlock);
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _link?.Dispose();
            _actionBlock?.Complete();
            _link = null;
            _actionBlock = null;
        }

        public void Dispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                Complete();
                Buffer.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("Dispose: {e}", e);
            }
        }

        public void Complete()
        {
        }

        public void Fault(Exception exception)
        {
        }

        public Task Completion => _actionBlock.Completion;

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, AudioDataMessage messageValue,
            ISourceBlock<AudioDataMessage> source, bool consumeToAccept)
        {
            return ((ITargetBlock<AudioDataMessage>)_bufferBlock).OfferMessage(messageHeader, messageValue, source,
                consumeToAccept);
        }
    }
}