using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Blocks
{
    public struct AudioOutputBlockConfig
    {
        public AudioFormat AudioFormat;
        public int NodeCount;

    }
    public sealed class AudioOutputBlock : ITargetBlock<AudioDataMessage>, ILifecycleDevice<AudioOutputBlockConfig, bool>
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
        private CircularBuffer _buffer;
        private CancellationTokenSource _cancellationTokenSource;

        public CircularBuffer Buffer { get; private set; }

        public AudioFormat InputFormat { get; private set; }

        public LifecyclePhase Phase { get; set; }

        public AudioOutputBlock()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioOutputBlock>();
        }

        public void ExceptionHappened(Exception e, string method)
        {
            throw e;
        }

        public Task<bool> CreateResources(AudioOutputBlockConfig config)
        {
            InputFormat = config.AudioFormat;
            
            var name = $"Output Buffer {AudioService.Instance.Graph.GetNextId()}";
            _buffer = new CircularBuffer(name, config.NodeCount, 4 * InputFormat.BufferSize);
            Buffer = new CircularBuffer(name);
            return Task.FromResult(true);
        }


        public Task<bool> StartProcessing()
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
            
            return Task.FromResult(true);
        }

        public Task<bool> FreeResources()
        {
            _buffer.Dispose();
            
            return Task.FromResult(true);
        }
        
        public Task<bool> StopProcessing()
        {
            Complete();
            _link.Dispose();
            _cancellationTokenSource.Cancel();
            try
            {
                Task.WaitAll(new Task[] { Completion });
            }
            catch (TaskCanceledException e)
            {
            }
            catch (AggregateException e)
            {
            }
            _link = null;
            _actionBlock = null;
            _logger.Information("DONE");
            return Task.FromResult(true);
        }

        public void Dispose() => Dispose(true);

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            AudioService.Instance.Logger.Information("Dispose called for OutputBlock {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Buffer.Dispose();
                }

                _disposedValue = disposing;
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
            return ((ITargetBlock<AudioDataMessage>)_bufferBlock).OfferMessage(messageHeader, messageValue, source,
                consumeToAccept);
        }
    }
}