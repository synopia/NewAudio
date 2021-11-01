using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Blocks
{
    public abstract class BaseAudioBlock: IPropagatorBlock<IAudioMessage, IAudioMessage>, IDisposable
    {
        protected readonly ILogger Logger;

        protected readonly AudioDataflow Flow;
        public readonly BufferBlock<IAudioMessage> Source;
        public readonly ITargetBlock<IAudioMessage> Target;
        public LifecyclePhase CurrentPhase { get; private set; }
        public bool IsPostDataRequestMessages => DataRequestSource != null;
        public bool IsPostDataResponseMessages => DataResponseSource != null;

        protected abstract bool IsForwardLifecycleMessages { get; }
        protected abstract ITargetBlock<AudioDataMessage> DataResponseSource { get; }
        protected abstract ITargetBlock<AudioDataRequestMessage> DataRequestSource { get; }
        public Action<LifecyclePhase, LifecyclePhase> PhaseChanged;
        protected BaseAudioBlock(AudioDataflow flow)
        {
            Logger = AudioService.Instance.Logger;
            Logger.Information("Constructing {this}", this);
            Flow = flow;
            Flow.Add(this);
            Source =new BufferBlock<IAudioMessage>();
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
            Target.Completion.ContinueWith(delegate { Source.Complete(); });
        }


        public virtual void Dispose()
        {
            CurrentPhase = LifecyclePhase.Shutdown;
            Flow.Remove(this);
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

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, IAudioMessage messageValue,
            ISourceBlock<IAudioMessage> source, bool consumeToAccept)
        {
            return Target.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public IDisposable LinkTo(ITargetBlock<IAudioMessage> target, DataflowLinkOptions linkOptions)
        {
            return Source.LinkTo(target, linkOptions);
        }

        public IAudioMessage ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<IAudioMessage> target, out bool messageConsumed)
        {
            return ((IReceivableSourceBlock<IAudioMessage>)Source).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<IAudioMessage> target)
        {
            return ((IReceivableSourceBlock<IAudioMessage>)Source).ReserveMessage(messageHeader, target);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<IAudioMessage> target)
        {
            ((IReceivableSourceBlock<IAudioMessage>)Source).ReleaseReservation(messageHeader, target);

        }
        
    }
}