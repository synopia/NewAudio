using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;

namespace NewAudio.Blocks
{
    public class MessageFilterBlock<TOut>: IReceivableSourceBlock<TOut>, ITargetBlock<IAudioMessage> where TOut:IAudioMessage
    {
        private TransformBlock<IAudioMessage, TOut> _transformBlock;

        public IReceivableSourceBlock<TOut> SourceBlock => _transformBlock;
        public ITargetBlock<IAudioMessage> TargetBlock => _transformBlock;

        public MessageFilterBlock()
        {
            _transformBlock = new TransformBlock<IAudioMessage, TOut>(msg => (TOut)msg);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, IAudioMessage messageValue,
            ISourceBlock<IAudioMessage> source, bool consumeToAccept)
        {
            if (messageValue is TOut)
            {
                return TargetBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
            }

            return DataflowMessageStatus.Declined;
        }

        public void Complete()
        {
            _transformBlock.Complete();
        }

        public void Fault(Exception exception)
        {
            SourceBlock.Fault(exception);
        }

        public Task Completion => _transformBlock.Completion;
        
        public IDisposable LinkTo(ITargetBlock<TOut> target, DataflowLinkOptions linkOptions)
        {
            return _transformBlock.LinkTo(target, linkOptions);
        }

        public TOut ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOut> target, out bool messageConsumed)
        {
            return SourceBlock.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOut> target)
        {
            return SourceBlock.ReserveMessage(messageHeader, target);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TOut> target)
        {
            SourceBlock.ReleaseReservation(messageHeader, target);
        }

        public bool TryReceive(Predicate<TOut> filter, out TOut item)
        {
            return SourceBlock.TryReceive(filter, out item);
        }

        public bool TryReceiveAll(out IList<TOut> items)
        {
            return SourceBlock.TryReceiveAll(out items);
        }
    }
}