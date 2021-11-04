using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Blocks
{
    public class AudioInputBlock : ISourceBlock<AudioDataMessage>, IAudioBlock
    {
        private readonly ITargetBlock<AudioDataMessage> _bufferBlock = new BufferBlock<AudioDataMessage>(
            new DataflowBlockOptions
            {
                BoundedCapacity = 100,
                MaxMessagesPerTask = 4
            });

        private readonly ILogger _logger;
        private readonly CircularBuffer _buffer;
        public CircularBuffer Buffer { get; }

        public AudioFormat OutputFormat { get; set; }
        private CancellationTokenSource _cancellationTokenSource;
        private Task _task;

        public AudioInputBlock(AudioFormat outputFormat)
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioInputBlock>();
            try
            {
                var name = $"Input Block {AudioService.Instance.Graph.GetNextId()}";
                Buffer = new CircularBuffer(name, 32, 4 * outputFormat.BufferSize);
                _buffer = new CircularBuffer(name);

                OutputFormat = outputFormat;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }
        }

        public void Play()
        {
            if (_task != null)
            {
                _logger.Warning("Task != null {task}", _task);
            }
            _cancellationTokenSource = new CancellationTokenSource();
            _task = Task.Run(Loop, _cancellationTokenSource.Token);
        }

        private void Loop()
        {
            try
            {
                var token = _cancellationTokenSource.Token;
                _logger.Information("Audio input reading thread (Reading from {reading} ({owner}))", _buffer?.Name,
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

                    if (!token.IsCancellationRequested && pos != OutputFormat.BufferSize)
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
            catch (Exception e)
            {
                _logger.Error("{e}", e);
            }
        }
        
        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _task.Wait(1000);
            _logger.Information("Audio input reading thread finished");
            _task = null;
        }        
        
        public void Dispose() => Dispose(true);
        
        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _buffer.Dispose();
                }

                _disposedValue = disposing;
            }
        }
        
        /*
        protected AudioDataMessage CreateMessage(AudioDataRequestMessage request, int sampleCount)
        {
            AudioDataMessage output;
            if (!_reusedData && request.ReusableDate != null)
            {
                output = new AudioDataMessage(request.ReusableDate, Format, sampleCount / Format.Channels);
                _reusedData = true;
            }
            else
            {
                output = new AudioDataMessage(Format, sampleCount / Format.Channels);
            }

            return output;
        }
        */

        public void Complete()
        {
        }

        public void Fault(Exception exception)
        {
            throw new NotImplementedException();
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