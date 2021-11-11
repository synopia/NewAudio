using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.Lib.Basics.Resources;

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
        private readonly IResourceHandle<AudioService> _audioService;

        private ActionBlock<AudioDataMessage> _actionBlock;
        private CircularBuffer _buffer;
        public CircularBuffer Buffer { get; private set; }

        public AudioFormat InputFormat { get; private set; }

        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;

        private bool _firstLoop;
        private long _messagesReceived;

        public AudioOutputBlock(): this(VLApi.Instance){}
        public AudioOutputBlock(IVLApi api)
        {
            _audioService = api.GetAudioService();
            _logger = _audioService.Resource.GetLogger<AudioOutputBlock>();
        }

        public void Create(AudioFormat audioFormat, int nodeCount)
        {
            InputFormat = audioFormat;

            var name = $"Output Buffer {_audioService.Resource.GetNextId()}";
            _buffer = new CircularBuffer(name, nodeCount, 4 * InputFormat.BufferSize);
            Buffer = new CircularBuffer(name);

            Start();
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

        private bool Stop()
        {
            if (_actionBlock == null)
            {
                _logger.Error("ActionBlock == null!");
                return false;
            }
            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");
            }
            _cancellationTokenSource.Cancel();
            var completion = _actionBlock.Completion;
            if (completion.Status == TaskStatus.Running)
            {
                var t = completion.ContinueWith(t =>
                {
                    _logger.Information("ActionBlock stopped, status={status}", t.Status);
                    _actionBlock = null;
                    return true;
                });
                _actionBlock.Complete();
                return t.GetAwaiter().GetResult();
            }

            return true;
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
                else
                {
                    _messagesReceived++;
                }
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = 1,
                CancellationToken = _token
            });
        }

        public string DebugInfo()
        {
            return $"{_buffer?.Name}, recv={_messagesReceived}";
        }

        public void Dispose() => Dispose(true);

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            _logger.Information("Dispose called for OutputBlock {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Stop();
                    Buffer.Dispose();
                    _audioService.Dispose();
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