using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Blocks
{
    public abstract class BaseAudioBlock<TIn, TOut> : IPropagatorBlock<TIn, TOut>, IDisposable
    {
        protected readonly AudioDataflow Flow;
        protected readonly ILogger Logger;

        public Action<LifecyclePhase, LifecyclePhase> PhaseChanged;

        protected BaseAudioBlock(AudioDataflow flow)
        {
            Logger = AudioService.Instance.Logger;
            Logger.Information("Constructing {this}", this);
            Flow = flow;

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

        public ISourceBlock<TOut> Source { get; protected set; }
        public ITargetBlock<TIn> Target { get; protected set; }
        public LifecyclePhase CurrentPhase { get; private set; }


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

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TIn messageValue,
            ISourceBlock<TIn> source, bool consumeToAccept)
        {
            return Target.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public IDisposable LinkTo(ITargetBlock<TOut> target, DataflowLinkOptions linkOptions)
        {
            return Source.LinkTo(target, linkOptions);
        }

        public TOut ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOut> target,
            out bool messageConsumed)
        {
            return ((IReceivableSourceBlock<TOut>)Source).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOut> target)
        {
            return ((IReceivableSourceBlock<TOut>)Source).ReserveMessage(messageHeader, target);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TOut> target)
        {
            ((IReceivableSourceBlock<TOut>)Source).ReleaseReservation(messageHeader, target);
        }
    }
}