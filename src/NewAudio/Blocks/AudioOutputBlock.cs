using System;
using System.Buffers;
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

    public sealed class AudioOutputBlock : ITargetBlock<AudioDataMessage>
    {
        private readonly ILogger _logger;

        private ActionBlock<AudioDataMessage> _actionBlock;
        private CircularBuffer _buffer;
        public CircularBuffer Buffer { get; private set; }

        public AudioFormat InputFormat { get; private set; }

        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;
        public int? InputBufferCount => _actionBlock?.InputCount;

        private bool _firstLoop;

        public AudioOutputBlock()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioOutputBlock>();
        }

        public void Create(AudioFormat audioFormat, int nodeCount)
        {
            InputFormat = audioFormat;

            var name = $"Output Buffer {AudioService.Instance.Graph.GetNextId()}";
            _buffer = new CircularBuffer(name, nodeCount, 4 * InputFormat.BufferSize);
            Buffer = new CircularBuffer(name);

            Start();
        }

        public async Task<bool> Free()
        {
            await Stop();
            _buffer.Dispose();
            return true;
        }


        private void Start()
        {
            if (_actionBlock != null)
            {
                _logger.Warning("ActionBlock != null!");
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _token = _cancellationTokenSource.Token;
            _firstLoop = true;
            
            CreateActionBlock();
        }

        private Task<bool> Stop()
        {
            if (_actionBlock == null)
            {
                _logger.Error("ActionBlock == null!");
                return Task.FromResult(false);
            }
            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");
            }
            _cancellationTokenSource.Cancel();
            _actionBlock.Complete();

            return _actionBlock.Completion.ContinueWith(t =>
            {
                _logger.Information("ActionBlock stopped, status={status}", t.Status);
                _actionBlock = null;
                return true;
            });
        }

        private void CreateActionBlock()
        {
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

                while (pos < message.BufferSize && !_token.IsCancellationRequested)
                {
                    var v = _buffer.Write(message.Data, pos, 1);
                    pos += v;
                }

                ArrayPool<float>.Shared.Return(message.Data);
                if (!_token.IsCancellationRequested && pos != message.BufferSize)
                {
                    _logger.Warning("pos!=msg {pos}!={msg}", pos, message.BufferSize);
                }
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = 2,
                CancellationToken = _token
            });
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
                    _cancellationTokenSource?.Cancel();
                    _actionBlock?.Complete();
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
            return ((ITargetBlock<AudioDataMessage>)_actionBlock).OfferMessage(messageHeader, messageValue, source,
                consumeToAccept);
        }
    }
}