using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Internal;
using Serilog;

namespace NewAudio
{
    /// <summary>
    /// Converts audio buffers of different sizes. 
    /// Writes incoming data into an internal ring buffer.
    ///
    /// AudioFlowSource: Sends out newly created buffers of a fixed length.
    /// AudioFlowSink: Is used as a ISampleProvider to manually read from. 
    /// </summary>
    public abstract  class AudioFlowBuffer : IPropagatorBlock<AudioBuffer, AudioBuffer>, IReceivableSourceBlock<AudioBuffer>, IDisposable, ISampleProvider
    {
        protected readonly ILogger Logger = Log.ForContext<AudioFlowBuffer>();
        private readonly BufferedSampleProvider _buffer = new BufferedSampleProvider();
        protected readonly BufferBlock<AudioBuffer> Source;
        private readonly ITargetBlock<AudioBuffer> _target;
        
        public AudioFormat Format
        {
            get;
        }
        public WaveFormat WaveFormat => Format.WaveFormat;
        
        public BufferedSampleProvider Buffer => _buffer;

        public AudioFlowBuffer(AudioFormat format, int internalBufferSize)
        {
            var source = new BufferBlock<AudioBuffer>(new DataflowBlockOptions()
            {
                // BoundedCapacity = 1,
                // MaxMessagesPerTask = 1,
                // EnsureOrdered = true
            });
            var target = new ActionBlock<AudioBuffer>(input =>
            {
                Logger.Verbose("receiving {count} at {time}, buffered: {SourceCount}", input.Count, input.Time, Source?.Count);
                _buffer.AddSamples( input.Data, 0, input.Count);
                AudioCore.Instance.BufferFactory.Release(input);

                OnDataReceived(input.Time, input.Count);
            }, new ExecutionDataflowBlockOptions()
            {
                // todo
                // BoundedCapacity = 1,
                // SingleProducerConstrained = false,
                // MaxDegreeOfParallelism = 1,
                // MaxMessagesPerTask = 1,
                // EnsureOrdered = true,
                
            });

            target.Completion.ContinueWith(delegate
            {
                source.Complete();
            });
            Format = format;
            _buffer.WaveFormat = WaveFormat;
            _buffer.BufferLength = internalBufferSize;
            Source = source;
            _target = target;
        }

        protected abstract void OnDataReceived(int time, int count);
        
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
            Logger.Verbose("TryReceive");
            return Source.TryReceive(filter, out item);
        }

        public bool TryReceiveAll(out IList<AudioBuffer> items)
        {
            Logger.Verbose("TryReceiveAll");
            return Source.TryReceiveAll(out items);
        }

        public IDisposable LinkTo(ITargetBlock<AudioBuffer> target, DataflowLinkOptions linkOptions)
        {
            Logger.Verbose("LinkTo {target}", target);
            return Source.LinkTo(target, linkOptions);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<AudioBuffer> target)
        {
            Logger.Verbose("Reserve Message {messageHeaderId}", messageHeader.Id);
            return ((IReceivableSourceBlock<AudioBuffer>)Source).ReserveMessage(messageHeader, target);
        }

        public AudioBuffer ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<AudioBuffer> target, out bool messageConsumed)
        {
            Logger.Verbose("Consume Message {messageHeaderId}", messageHeader.Id);
            return ((IReceivableSourceBlock<AudioBuffer>)Source).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<AudioBuffer> target)
        {
            Logger.Verbose("Release Message {messageHeaderId}", messageHeader.Id);
            ((IReceivableSourceBlock<AudioBuffer>)Source).ReleaseReservation(messageHeader, target);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, AudioBuffer messageValue, ISourceBlock<AudioBuffer> source,
            bool consumeToAccept)
        {
            if (messageValue.Count > _buffer.FreeSpace)
            {
                Logger.Warning(" FULL!");
                return DataflowMessageStatus.Postponed;
            }

            var status = _target.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
            Logger.Verbose("Offer Message {messageHeaderId}, {consumeToAccept} -> {status}", messageHeader.Id, consumeToAccept, status);
            return status;
        }

        public Task Completion => Source.Completion;
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

    public class AudioFlowSource : AudioFlowBuffer
    {
        private int _time = 0;
        private double _dTime = 0;
        
        public AudioFlowSource(AudioFormat format, int internalBufferSize) : base(format, internalBufferSize)
        {
        }

        protected override void OnDataReceived(int time, int count)
        {
            while (Buffer.BufferedSamples >= Format.BufferSize)
            {
                Logger.Verbose("OnDataReceived {count} at {_time}", count, _time);
                var buf = AudioCore.Instance.BufferFactory.GetBuffer(Format.BufferSize);

                Buffer.Read(buf.Data, 0, Format.BufferSize);
                buf.Time = _time;
                buf.DTime = _dTime;
                _time += Format.SampleCount;
                _dTime += 1.0 / Format.SampleRate;
                Source.Post(buf);
            }
        }
    }

    public class AudioFlowSink : AudioFlowBuffer
    {
        private int _time;
        public AudioFlowSink(AudioFormat format, int internalBufferSize) : base(format, internalBufferSize)
        {
        }

        protected override void OnDataReceived(int time, int count)
        {
            Logger.Verbose("Received {count} at {time}, buf={Buffer}  source count {SourceCount}", count, time, Buffer.BufferedSamples, Source.Count);
            if (time != _time)
            {
                Logger.Warning("TIME MISMATCH received: {time} should be:{_time}", time, _time);
                _time = time;
            }

            _time += count / Format.Channels;
        }
    }
}