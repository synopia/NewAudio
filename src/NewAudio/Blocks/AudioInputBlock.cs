using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Blocks
{
    public class AudioInputBlock : ISourceBlock<AudioDataMessage>, IDisposable
    {
        private readonly ILogger _logger;

        private readonly ITargetBlock<AudioDataMessage> _bufferBlock = new BufferBlock<AudioDataMessage>();
        public CircularBuffer Buffer { get; }

        public AudioFormat OutputFormat { get; set; }

        public AudioInputBlock(AudioDataflow flow, AudioFormat outputFormat)
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioInputBlock>();
            AudioService.Instance.Lifecycle.OnPlay += Loop;
            AudioService.Instance.Flow.Add(this);
            try
            {
                Buffer = new CircularBuffer($"Input Block {flow.GetId()}",64, outputFormat.BufferSize);
                OutputFormat = outputFormat;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }
        }
        
        private void Loop()
        {
            try
            {
                Task.Run(() =>
                {
                    _logger.Information("Audio input reading thread");
                    var token = AudioService.Instance.Lifecycle.GetToken();

                    while (!token.IsCancellationRequested)
                    {
                        var message = new AudioDataMessage(OutputFormat, OutputFormat.SampleCount);
                        var pos = 0;

                        while (pos < OutputFormat.BufferSize && !token.IsCancellationRequested)
                        {
                            var read = Buffer.Read(message.Data, pos);
                            pos += read;
                        }

                        _bufferBlock.Post(message);
                    }
                    _logger.Information("Audio input reading thread finished");

                });
            }
            catch (Exception e)
            {
                _logger.Error("{e}", e);
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
            _bufferBlock.Complete();
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