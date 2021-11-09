using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Blocks
{
    public struct DynamicBufferBlockLink : IDisposable
    {
        private DynamicBufferBlock _bufferBlock;

        public DynamicBufferBlockLink(DynamicBufferBlock bufferBlock)
        {
            _bufferBlock = bufferBlock;
        }

        public void Dispose()
        {
            _bufferBlock.DisposeLink();
        }
    }

    public class DynamicBufferBlock : IPropagatorBlock<AudioDataMessage, AudioDataMessage>,
        IReceivableSourceBlock<AudioDataMessage>, IDisposable
    {
        private readonly ILogger _logger = AudioService.Instance.Logger.ForContext<DynamicBufferBlock>();

        private BufferBlock<AudioDataMessage> _bufferBlock = new(new DataflowBlockOptions()
        {
            BoundedCapacity = 6
        });

        private DataflowBlockOptions _options = new DataflowBlockOptions()
        {
            BoundedCapacity = 6
        };

        private IDisposable _link1;

        private ITargetBlock<AudioDataMessage> _targetBlock;

        public int BufferUsage => _bufferBlock.Count;

        public void SetBlockOptions(DataflowBlockOptions options)
        {
            _options = options;

            _link1?.Dispose();
            _bufferBlock = new BufferBlock<AudioDataMessage>(_options);

            if (_targetBlock != null)
            {
                _link1 = _bufferBlock.LinkTo(_targetBlock);
            }
        }

        public void Complete()
        {
            _bufferBlock.Complete();
        }

        public void Fault(Exception exception)
        {
            throw exception;
        }

        public Task Completion => null;

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, AudioDataMessage messageValue,
            ISourceBlock<AudioDataMessage> source, bool consumeToAccept)
        {
            return ((ITargetBlock<AudioDataMessage>)_bufferBlock).OfferMessage(messageHeader, messageValue, source,
                consumeToAccept);
        }

        internal void DisposeLink()
        {
            _link1?.Dispose();
            _link1 = null;
            _targetBlock = null;
        }

        public IDisposable LinkTo(ITargetBlock<AudioDataMessage> target, DataflowLinkOptions linkOptions)
        {
            _link1?.Dispose();

            _targetBlock = target;
            _link1 = _bufferBlock.LinkTo(target, linkOptions);
            return new DynamicBufferBlockLink(this);
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

        public bool TryReceive(Predicate<AudioDataMessage> filter, out AudioDataMessage item)
        {
            return _bufferBlock.TryReceive(filter, out item);
        }

        public bool TryReceiveAll(out IList<AudioDataMessage> items)
        {
            return _bufferBlock.TryReceiveAll(out items);
        }

        private bool _disposedValue;

        public void Dispose()
        {
            AudioService.Instance.Logger.Information("Dispose called for DynamicBufferBlock {t}", this);
            if (!_disposedValue)
            {
                _link1?.Dispose();
                _link1 = null;
                _targetBlock = null;
                _disposedValue = true;
            }
        }
    }
}