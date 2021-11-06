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
    public struct AudioInputBlockConfig
    {
        public AudioFormat AudioFormat;
        public int NodeCount;
    }
    public class AudioInputBlock : ISourceBlock<AudioDataMessage>, ILifecycleDevice<AudioInputBlockConfig, bool>
    {
        private readonly ITargetBlock<AudioDataMessage> _bufferBlock = new BufferBlock<AudioDataMessage>(
            new DataflowBlockOptions
            {
                BoundedCapacity = 100,
                MaxMessagesPerTask = 4
            });

        private readonly ILogger _logger;
        private CircularBuffer _buffer;
        public CircularBuffer Buffer { get; private set; }
        public LifecyclePhase Phase { get; set; }

        public AudioFormat OutputFormat { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        private Task<bool> _task;

        public AudioInputBlock()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioInputBlock>();
        }

        public Task<bool> CreateResources(AudioInputBlockConfig config)
        {
            OutputFormat = config.AudioFormat;
            
            var name = $"Input Block {AudioService.Instance.Graph.GetNextId()}";
            Buffer = new CircularBuffer(name, config.NodeCount, 4 * config.AudioFormat.BufferSize);
            _buffer = new CircularBuffer(name);
            return Task.FromResult(true);
        }

        public Task<bool> FreeResources()
        {
            _buffer.Dispose();
            return Task.FromResult(true);
        }

        public Task<bool> StartProcessing()
        {
            if (_task != null)
            {
                _logger.Warning("Task != null {task}", _task);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _task = Task.Run(Loop, _cancellationTokenSource.Token);

            return Task.FromResult(true);
        }

        public Task<bool> StopProcessing()
        {
            _cancellationTokenSource.Cancel();
            Task.WaitAll(new Task[] { _task });
            return Task.FromResult(true);
        }

        public void ExceptionHappened(Exception e, string method)
        {
            throw e;
        }

        private bool Loop()
        {
            try
            {
                var token = _cancellationTokenSource.Token;
                _logger.Information("Audio input reading thread started (Reading from {reading} ({owner}))", _buffer?.Name,
                    _buffer?.IsOwnerOfSharedMemory);
                if (_buffer == null)
                {
                    throw new Exception("Buffer == null !");
                }

                while (!token.IsCancellationRequested)
                {
                    var message = new AudioDataMessage(OutputFormat, OutputFormat.SampleCount);
                    var pos = 0;

                    while (pos < OutputFormat.BufferSize && !token.IsCancellationRequested)
                    {
                        var read = _buffer.Read(message.Data, pos, 1);
                        pos += read;
                    }

                    if (!token.IsCancellationRequested)
                    {
                        if (pos != OutputFormat.BufferSize)
                        {
                            _logger.Warning("pos!=buf {p} {b} {t}", pos, OutputFormat.BufferSize,
                                token.IsCancellationRequested);
                        }
                        var res = _bufferBlock.Post(message);
                        _logger.Verbose("Posted {samples} ", message.BufferSize);
                        if (!res)
                        {
                            _logger.Warning("Cant deliver message");
                        }                        
                    }

                }
                _logger.Information("Audio input reading thread finished (Reading from {reading} ({owner}))", _buffer?.Name,
                    _buffer?.IsOwnerOfSharedMemory);
            }
            catch (Exception e)
            {
                _logger.Error("{e}", e);
            }

            return true;
        }

        public void Dispose() => Dispose(true);

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            AudioService.Instance.Logger.Information("Dispose called for InputBlock {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _buffer.Dispose();
                }

                _disposedValue = disposing;
            }
        }

        public void Complete()
        {
            throw new Exception("This should not be called!");
        }

        public void Fault(Exception exception)
        {
            throw new Exception("This should not be called!", exception);
        }

        public Task Completion => _bufferBlock.Completion;

        public IDisposable LinkTo(ITargetBlock<AudioDataMessage> target, DataflowLinkOptions linkOptions)
        {
            return ((ISourceBlock<AudioDataMessage>)_bufferBlock).LinkTo(target, linkOptions);
        }

        public AudioDataMessage ConsumeMessage(DataflowMessageHeader messageHeader,
            ITargetBlock<AudioDataMessage> target, out bool messageConsumed)
        {
            return ((ISourceBlock<AudioDataMessage>)_bufferBlock).ConsumeMessage(messageHeader, target,
                out messageConsumed);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<AudioDataMessage> target)
        {
            return ((ISourceBlock<AudioDataMessage>)_bufferBlock).ReserveMessage(messageHeader, target);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<AudioDataMessage> target)
        {
            ((ISourceBlock<AudioDataMessage>)_bufferBlock).ReleaseReservation(messageHeader, target);
        }
    }
}