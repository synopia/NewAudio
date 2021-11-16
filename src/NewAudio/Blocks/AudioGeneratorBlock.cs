using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Blocks
{
    public class AudioGeneratorBlock : ISourceBlock<AudioDataMessage>, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IResourceHandle<AudioService> _audioService;

        private ITargetBlock<AudioDataMessage> _outputBlock;
        public AudioFormat OutputFormat { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;
        private Thread _thread;

        public AudioGeneratorBlock() 
        {
            _audioService = Factory.GetAudioService();
            _logger = _audioService.Resource.GetLogger<AudioGeneratorBlock>();
        }

        public void Create(ITargetBlock<AudioDataMessage> outputBlock, AudioFormat format)
        {
            _outputBlock = outputBlock;
            OutputFormat = format;
            Start();
        }

        private void Start()
        {
            if (_thread != null)
            {
                _logger.Warning("Thread != null {Thread}", _thread.Name);
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

        public void Stop()
        {
            if (_thread == null)
            {
                _logger.Error("Thread == null");
                return;
            }

            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");
            }

            _cancellationTokenSource.Cancel();
            _thread.Join();
            _logger.Information("Audio generator thread finished");
        }

        private void Loop()
        {
            try
            {
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
                    var timeSent = samplesSent * 1000.0 / OutputFormat.SampleRate;
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
                _logger.Error(e, "Exception in AudioGenerator loop");
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource?.Cancel();
                    _thread.Join();
                    _audioService.Dispose();
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