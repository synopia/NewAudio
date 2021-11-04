using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Blocks
{
    public interface IAudioBlock: IDisposable
    {
        public void Play();
        public void Stop();

    }
    public abstract class AudioBlock : IAudioBlock, IPropagatorBlock<AudioDataMessage, AudioDataMessage>
    {
        protected AudioBlock()
        {
            /*Source =new BufferBlock<IAudioMessage>();
            Target = new ActionBlock<IAudioMessage>(input =>
            {
                try
                {
                    // Logger.Verbose("Received message {input}", input);
                    if (IsPostDataRequestMessages && input is AudioDataRequestMessage requestMessage)
                    {
                            DataRequestSource.Post(requestMessage);
                    } else if (IsPostDataResponseMessages && input is AudioDataMessage dataMessage)
                    {
                            DataResponseSource.Post(dataMessage);
                    } else if (input is LifecycleMessage lifecycleMessage)
                    {
                        if (CurrentPhase != lifecycleMessage.Enter)
                        {
                            PhaseChanged?.Invoke(CurrentPhase, lifecycleMessage.Enter);
                            CurrentPhase = lifecycleMessage.Enter;
                        }

                        if (IsForwardLifecycleMessages)
                        {
                            Source.Post(lifecycleMessage);
                        }
                        
                    }
                }
                catch (Exception e)
                {
                   Logger.Error("{e}", e);
                }
            }, new ExecutionDataflowBlockOptions()
            {
                
            });
            Target.Completion.ContinueWith(delegate { Source.Complete(); });*/
        }

        public ISourceBlock<AudioDataMessage> Source { get; protected set; }
        public ITargetBlock<AudioDataMessage> Target { get; protected set; }

        public void Play()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public virtual void Dispose()
        {
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