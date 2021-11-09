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
    
    public class AudioInputBlock : ISourceBlock<AudioDataMessage>
    {
        private readonly ILogger _logger;
        private ITargetBlock<AudioDataMessage> _outputBlock;

        private CircularBuffer _buffer;
        public CircularBuffer Buffer { get; private set; }
        public AudioFormat OutputFormat { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;
        private Thread _thread;

        public AudioInputBlock()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioInputBlock>();
        }

        public void Create(ITargetBlock<AudioDataMessage> outputBlock, AudioFormat audioFormat, int nodeCount)
        {
            OutputFormat = audioFormat;
            _outputBlock = outputBlock;
            
            var name = $"Input Block {AudioService.Instance.Graph.GetNextId()}";
            Buffer = new CircularBuffer(name, nodeCount, 4 * audioFormat.BufferSize);
            _buffer = new CircularBuffer(name);
            
            Start();
        }

        public async Task<bool> Free()
        {
            await Stop();
            Buffer.Dispose();
            return true;
        }

        private void Start()
        {
            if (_thread != null)
            {
                _logger.Warning("Thread != null {task}", _thread);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _token = _cancellationTokenSource.Token;
            
            _thread = new Thread(Loop)
            {
                Priority = ThreadPriority.AboveNormal,
                IsBackground = true
            };
            _thread.Start();
        }

        private Task<bool> Stop()
        {
            if (_thread== null)
            {
                _logger.Error("Thread == null {task}", _thread);
                return Task.FromResult(false);
            }

            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");
            }
            _cancellationTokenSource.Cancel();
            _thread.Join();
            _logger.Information("Audio input reading thread finished");
            return Task.FromResult(true);
        }

        private void Loop()
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
                        if (!res)
                        {
                            ArrayPool<float>.Shared.Return(message.Data);
                        }
                        _logger.Verbose("Posted {samples} ", message.BufferSize);
                    }

                }
            }
            catch (Exception e)
            {
                _logger.Error("{e}", e);
            }

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
                    _thread.Join();
                    Buffer.Dispose();
                    _thread = null;
                    Buffer = null;
                }

                _disposedValue = disposing;
            }
        }

        public void Complete()
        {
            Stop();
        }

        public void Fault(Exception exception)
        {
            _logger.Error("{e}", exception);
            _thread.Abort();
        }

        public Task Completion => Task.Run(() => _thread.Join());

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