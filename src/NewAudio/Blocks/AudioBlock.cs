using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Blocks
{
    public abstract class AudioBlock : IPropagatorBlock<AudioDataMessage, AudioDataMessage>
    {
        private IResourceHandle<AudioService> _audioService;
        protected ILogger Logger;

        protected AudioService AudioService => _audioService.Resource;

        protected AudioBlock(): this(VLApi.Instance)
        {
        }

        private AudioBlock(IVLApi api)
        {
            _audioService = api.GetAudioService();
        }
        
        public void InitLogger<T>()
        {
            Logger = AudioService.GetLogger<T>();
        }


        public abstract ISourceBlock<AudioDataMessage> Source { get; set; }
        public abstract ITargetBlock<AudioDataMessage> Target { get; set; }
        public AudioFormat OutputFormat { get; protected set; }


        private bool _disposedValue;
        
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _audioService.Dispose();
                }

                _disposedValue = true;
            }
        }
        public void Complete()
        {
            Target.Complete();
        }

        public void Fault(Exception exception)
        {
            Target.Fault(exception);
        }

        public Task Completion => Source.Completion;

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, AudioDataMessage messageValue,
            ISourceBlock<AudioDataMessage> source, bool consumeToAccept)
        {
            return Target.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public IDisposable LinkTo(ITargetBlock<AudioDataMessage> target, DataflowLinkOptions linkOptions)
        {
            return Source.LinkTo(target, linkOptions);
        }

        public AudioDataMessage ConsumeMessage(DataflowMessageHeader messageHeader,
            ITargetBlock<AudioDataMessage> target,
            out bool messageConsumed)
        {
            return ((IReceivableSourceBlock<AudioDataMessage>)Source).ConsumeMessage(messageHeader, target,
                out messageConsumed);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<AudioDataMessage> target)
        {
            return ((IReceivableSourceBlock<AudioDataMessage>)Source).ReserveMessage(messageHeader, target);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<AudioDataMessage> target)
        {
            ((IReceivableSourceBlock<AudioDataMessage>)Source).ReleaseReservation(messageHeader, target);
        }
    }
}