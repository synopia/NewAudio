using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Blocks
{
    public interface IAudioBlock: IDisposable, ILifecycleDevice
    {
    }
    public abstract class AudioBlock : IAudioBlock, IPropagatorBlock<AudioDataMessage, AudioDataMessage>
    {
        public LifecyclePhase Phase { get; set; }
        public readonly LifecycleStateMachine Lifecycle = new LifecycleStateMachine();

        protected AudioBlock()
        {
            Lifecycle.EventHappens(LifecycleEvents.eCreate, this);
        }

        public ISourceBlock<AudioDataMessage> Source { get; protected set; }
        public ITargetBlock<AudioDataMessage> Target { get; protected set; }

        public void ExceptionHappened(Exception e, string method)
        {
            throw e;
        }

        public abstract bool CreateResources();
        public abstract bool FreeResources();
        public abstract bool StartProcessing();
        public abstract bool StopProcessing();

        private bool _disposedValue;
        
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
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