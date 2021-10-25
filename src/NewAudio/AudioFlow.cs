using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Internal;

namespace NewAudio
{
    /// <summary>
    /// Converts audio buffers of different sizes. 
    /// Writes incoming buffers into an internal ring buffer and sends out newly created buffers of a fixed length.
    /// Sending can be disabled and instead use it as a ISampleProvider. 
    /// </summary>
    public class AudioFlowBuffer : IPropagatorBlock<AudioBuffer, AudioBuffer>, IReceivableSourceBlock<AudioBuffer>, IDisposable, ISampleProvider
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioDataflow");
        private readonly int _sendBufferSize;
        private readonly BufferedSampleProvider _buffer = new BufferedSampleProvider();
        private readonly BufferBlock<AudioBuffer> _source;
        private readonly ITargetBlock<AudioBuffer> _target;

        public AudioFormat Format
        {
            get;
        }
        public WaveFormat WaveFormat => Format.WaveFormat;
        
        public BufferedSampleProvider Buffer => _buffer;

        public AudioFlowBuffer(AudioFormat format, int internalBufferSize, int sendBufferSize=0)
        {
            _buffer.Name = _logger.Category;
            _logger.Info($"Starting flow buffer internal size: {internalBufferSize}, sending size: {sendBufferSize}");
            var source = new BufferBlock<AudioBuffer>(new DataflowBlockOptions()
            {
            });
            var target = new ActionBlock<AudioBuffer>(input =>
            {
                // TODO
                // if (_buffer.FreeSpace >= input.Count)
                // {
                    _buffer.AddSamples(input.Time, input.Data, 0, input.Count);
                    // input.Owner?.Release(input);
                // }

                while (sendBufferSize>0 && _buffer.BufferedSamples >= sendBufferSize)
                {
                    SendBuffer();
                }
            });

            target.Completion.ContinueWith(delegate
            {
                while (sendBufferSize>0 && _buffer.BufferedSamples >= sendBufferSize)
                {
                    SendBuffer();
                }
                source.Complete();
            });
            Format = format;
            _buffer.WaveFormat = WaveFormat;
            _buffer.BufferLength = internalBufferSize;
            _sendBufferSize = sendBufferSize;
            _source = source;
            _target = target;
        }

        private void SendBuffer()
        {
            if (_sendBufferSize==0)
            {
                throw new ArgumentException("Send buffer size is 0!");
            }
            var buf = AudioCore.Instance.BufferFactory.GetBuffer(_buffer.ReadTime, _sendBufferSize);
            _buffer.Read(buf.Data, 0, _sendBufferSize);
            _source.Post(buf);
        }
        
        public void Dispose()
        {
            _buffer.Dispose();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return _buffer.Read(buffer, offset, count);
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