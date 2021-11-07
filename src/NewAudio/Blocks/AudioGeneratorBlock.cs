using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Blocks
{
    public class AudioGeneratorBlock : ISourceBlock<AudioDataMessage>
    {
        private readonly ILogger _logger;
        private readonly ITargetBlock<AudioDataMessage> _outputBlock = new BufferBlock<AudioDataMessage>(
            new DataflowBlockOptions
            {
                BoundedCapacity = 16,
                MaxMessagesPerTask = 16,
            });
        public AudioFormat OutputFormat { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;
        private Thread _thread;
        private float[] _bufferToSend;

        public AudioGeneratorBlock()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioGeneratorBlock>();
            
        }

        public bool Create(AudioFormat format)
        {
            OutputFormat = format;

            _bufferToSend = new float[format.BufferSize];
            Start();
            return true;
        }
        
        public async Task<bool> Free()
        {
            await Stop();
            return true;
        }

        private void Start()
        {
            if (_thread != null)
            {
                _logger.Warning("Thread != null {thread}", _thread);
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
            if (_thread == null)
            {
                _logger.Error("Thread == null {thread}", _thread);
                return Task.FromResult(false);
            }

            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");

            }
            _cancellationTokenSource.Cancel();
            
            return Task.FromResult(true);
        }

        private void Loop()
        {
            try
            {
                var bytesPerSecond = OutputFormat.WaveFormat.AverageBytesPerSecond;
                var messagesPerSecond = bytesPerSecond / 4 / OutputFormat.BufferSize;
                long samplesSent = 0;
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                while (!_token.IsCancellationRequested)
                {
                    var message = new AudioDataMessage(OutputFormat, OutputFormat.SampleCount);
                    var res = _outputBlock.Post(message);
                    if (!res)
                    {
                        ArrayPool<float>.Shared.Return(message.Data);
                    }

                    double timeDiff;
                    samplesSent += message.SampleCount;
                    var timeSent = (double)samplesSent * 1000.0 / OutputFormat.SampleRate;
                    do
                    {
                        timeDiff = timeSent - stopwatch.Elapsed.TotalMilliseconds;
                        if (timeDiff > 1.0)
                        {
                            Thread.Sleep(1);
                        }
                    } while (timeDiff > 0.1 && !_token.IsCancellationRequested);

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
            AudioService.Instance.Logger.Information("Dispose called for AudioGeneratorBlock {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource?.Cancel();
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