using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Internal;

namespace NewAudio
{
    public class AudioFlowBuffer : IPropagatorBlock<AudioBuffer, AudioBuffer>, IReceivableSourceBlock<AudioBuffer>, IAudioBufferOwner, IDisposable
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioDataflow");
        private readonly int _blockCount;
        private readonly int _bufferSize;
        private readonly BufferedSampleProvider _buffer = new BufferedSampleProvider();
        private readonly BufferBlock<AudioBuffer> _source;
        private readonly ITargetBlock<AudioBuffer> _target;
        private readonly Queue<AudioBuffer> _buffers = new Queue<AudioBuffer>();
        private readonly bool _sendBuffers;

        public BufferedSampleProvider Buffer => _buffer;
        public int BufferUsed => _source.Count;
        public int CachedBuffers => _buffers.Count;

        public AudioFlowBuffer(int bufferSize, int blockCount, bool sendBuffers=false)
        {
            _logger.Info($"Starting flow buffer size: {bufferSize}, block count: {blockCount}, send buffers: {sendBuffers}");
            var source = new BufferBlock<AudioBuffer>(new DataflowBlockOptions()
            {
            });
            var target = new ActionBlock<AudioBuffer>(input =>
            {
                if (_buffer.FreeSpace >= input.Size)
                {
                    _buffer.AddSamples(input.Data, 0, input.Size);
                    input.Owner?.Release(input);
                }

                while (sendBuffers && _buffer.BufferedSamples >= bufferSize)
                {
                    SendBuffer();
                }
            });

            target.Completion.ContinueWith(delegate
            {
                while (sendBuffers && _buffer.BufferedSamples >= bufferSize)
                {
                    SendBuffer();
                }
                source.Complete();
            });

            _buffer.BufferLength = bufferSize*blockCount;
            _blockCount = blockCount;
            _bufferSize = bufferSize;
            _sendBuffers = sendBuffers;
            _source = source;
            _target = target;
        }

        private void SendBuffer()
        {
            if (!_sendBuffers)
            {
                throw new ArgumentException("Use bool sendBuffers in order to send buffers");
            }
            var buf = GetBuffer();
            _buffer.Read(buf.Data, 0, _bufferSize);
            _source.Post(buf);
        }

        public void Release(AudioBuffer buffer)
        {
            _buffers.Enqueue(buffer);
        }
        private AudioBuffer GetBuffer()
        {
            if (!_sendBuffers)
            {
                throw new ArgumentException("Use bool sendBuffers in order to send buffers");
            }
            if (_buffers.Count > 0)
            {
                return _buffers.Dequeue();
            }

            return new AudioBuffer(this, _bufferSize);
        }

        public void Dispose()
        {
            _buffer.Dispose();
        }

        public bool TryReceive(Predicate<AudioBuffer> filter, out AudioBuffer item)
        {
            return _source.TryReceive(filter, out item);
        }

        public bool TryReceiveAll(out IList<AudioBuffer> items)
        {
            return _source.TryReceiveAll(out items);
        }

        public IDisposable LinkTo(ITargetBlock<AudioBuffer> target, DataflowLinkOptions linkOptions)
        {
            return _source.LinkTo(target, linkOptions);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<AudioBuffer> target)
        {
            return ((IReceivableSourceBlock<AudioBuffer>)_source).ReserveMessage(messageHeader, target);
        }

        public AudioBuffer ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<AudioBuffer> target, out bool messageConsumed)
        {
            return ((IReceivableSourceBlock<AudioBuffer>)_source).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<AudioBuffer> target)
        {
            ((IReceivableSourceBlock<AudioBuffer>)_source).ReleaseReservation(messageHeader, target);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, AudioBuffer messageValue, ISourceBlock<AudioBuffer> source,
            bool consumeToAccept)
        {
            return _target.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public Task Completion => _source.Completion;
        public void Complete()
        {
            _target.Complete();
        }

        public void Fault(Exception exception)
        {
            _target.Fault(exception);
        }

        public void ClearBuffer()
        {
            _buffer.ClearBuffer();
        }
    }
    
    
}