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
    public class AudioInputBlock : ISourceBlock<AudioDataMessage>
    {
        private readonly ILogger _logger;
        private readonly ITargetBlock<AudioDataMessage> _outputBlock = new BufferBlock<AudioDataMessage>(
            new DataflowBlockOptions
            {
                BoundedCapacity = 100,
                MaxMessagesPerTask = 4
            });

        private CircularBuffer _buffer;
        public CircularBuffer Buffer { get; private set; }
        public AudioFormat OutputFormat { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;
        private Task<bool> _task;

        public AudioInputBlock()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioInputBlock>();
        }

        public bool Create(AudioInputBlockConfig config)
        {
            OutputFormat = config.AudioFormat;
            
            var name = $"Input Block {AudioService.Instance.Graph.GetNextId()}";
            Buffer = new CircularBuffer(name, config.NodeCount, 4 * config.AudioFormat.BufferSize);
            _buffer = new CircularBuffer(name);
            
            Start();

            return true;
        }

        public async Task<bool> Free()
        {
            await Stop();
            Buffer.Dispose();
            return true;
        }

        private void Start()
        {
            if (_task != null)
            {
                _logger.Warning("Task != null {task}", _task);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _token = _cancellationTokenSource.Token;
            
            _task = Task.Run(Loop, _token);
            // var thread = new Thread(Loop);
            
            // thread.Priority = ThreadPriority.AboveNormal;
            // thread.IsBackground = true;
            
        }

        private Task<bool> Stop()
        {
            if (_task == null)
            {
                _logger.Error("Task == null {task}", _task);
                return Task.FromResult(false);
            }

            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");
            }
            _cancellationTokenSource.Cancel();
            return _task.ContinueWith(t =>
            {
                _logger.Information("Task stopped, status={status}", t.Status);
                _task = null;
                return true;
            });
        }

        private bool Loop()
        {
            try
            {
                _logger.Information("Audio input reading thread started (Reading from {reading} ({owner}))", _buffer?.Name,
                    _buffer?.IsOwnerOfSharedMemory);
                if (_buffer == null)
                {
                    throw new Exception("Buffer == null !");
                }

                while (!_token.IsCancellationRequested)
                {
                    var message = new AudioDataMessage(OutputFormat, OutputFormat.SampleCount);
                    var pos = 0;

                    while (pos < OutputFormat.BufferSize && !_token.IsCancellationRequested)
                    {
                        var read = _buffer.Read(message.Data, pos, 1);
                        pos += read;
                    }

                    if (!_token.IsCancellationRequested)
                    {
                        if (pos != OutputFormat.BufferSize)
                        {
                            _logger.Warning("pos!=buf {p} {b} {t}", pos, OutputFormat.BufferSize,
                                _token.IsCancellationRequested);
                        }
                        var res = _outputBlock.Post(message);
                        _logger.Verbose("Posted {samples} ", message.BufferSize);
                        if (!res)
                        {
                            _logger.Warning("Cant deliver message");
                        }                        
                    }

                }
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
                    _cancellationTokenSource?.Cancel();
                    Buffer.Dispose();
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

        public Task Completion => _outputBlock.Completion;

        public IDisposable LinkTo(ITargetBlock<AudioDataMessage> target, DataflowLinkOptions linkOptions)
        {
            return ((ISourceBlock<AudioDataMessage>)_outputBlock).LinkTo(target, linkOptions);
        }

        public AudioDataMessage ConsumeMessage(DataflowMessageHeader messageHeader,
            ITargetBlock<AudioDataMessage> target, out bool messageConsumed)
        {
            return ((ISourceBlock<AudioDataMessage>)_outputBlock).ConsumeMessage(messageHeader, target,
                out messageConsumed);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<AudioDataMessage> target)
        {
            return ((ISourceBlock<AudioDataMessage>)_outputBlock).ReserveMessage(messageHeader, target);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<AudioDataMessage> target)
        {
            ((ISourceBlock<AudioDataMessage>)_outputBlock).ReleaseReservation(messageHeader, target);
        }
    }
}