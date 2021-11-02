using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Blocks
{
    public class AudioInputBlock : ISourceBlock<AudioDataMessage>, IDisposable
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

        public AudioInputBlock(AudioDataflow flow, AudioFormat outputFormat)
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioInputBlock>();
            AudioService.Instance.Lifecycle.OnPlay += StartLoop;
            AudioService.Instance.Flow.Add(this);
            try
            {
                var name = $"Input Block {flow.GetId()}";
                Buffer = new CircularBuffer(name, 32, 4 * outputFormat.BufferSize);
                _buffer = new CircularBuffer(name);

                OutputFormat = outputFormat;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }
        }


        public void Dispose()
        {
            try
            {
                AudioService.Instance.Flow.Remove(this);
                Buffer.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("Dispose: {e}", e);
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

        private void StartLoop()
        {
            Task.Run(Loop, AudioService.Instance.Lifecycle.GetToken());
            // Task.Run(Loop, AudioService.Instance.Lifecycle.GetToken());
            // Task.WaitAll(tasks, AudioService.Instance.Lifecycle.GetToken());
        }

        private void Loop()
        {
            try
            {
                _logger.Information("Audio input reading thread (Reading from {reading} ({owner}))", _buffer.Name,
                    _buffer.IsOwnerOfSharedMemory);
                var token = AudioService.Instance.Lifecycle.GetToken();
                while (!token.IsCancellationRequested)
                {
                    token = AudioService.Instance.Lifecycle.GetToken();
                    var message = new AudioDataMessage(OutputFormat, OutputFormat.SampleCount);
                    var pos = 0;

                    while (pos < OutputFormat.BufferSize && !token.IsCancellationRequested)
                    {
                        var read = _buffer.Read(message.Data, pos, 1);
                        pos += read;
                    }

                    if (!token.IsCancellationRequested && pos != OutputFormat.BufferSize)
                        _logger.Warning("pos!=buf {p} {b} {t}", pos, OutputFormat.BufferSize, token.IsCancellationRequested);
                    var res = _bufferBlock.Post(message);
                    _logger.Verbose("Posted {samples} ", message.BufferSize);
                    if (!res) _logger.Warning("Cant deliver message");
                }
            }
            catch (Exception e)
            {
                _logger.Error("{e}", e);
            }

            _logger.Information("Audio input reading thread finished");
        }
    }
}