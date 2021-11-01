using System.Threading.Tasks.Dataflow;
using NewAudio.Core;

namespace NewAudio.Blocks
{
    public class AudioEffectBlock : BaseAudioBlock
    {
        protected override bool IsForwardLifecycleMessages => true;

        private ActionBlock<AudioDataMessage> Processor;
        protected override ITargetBlock<AudioDataMessage> DataResponseSource => Processor; 
        protected override ITargetBlock<AudioDataRequestMessage> DataRequestSource => null;
        
        public AudioEffectBlock(AudioDataflow flow) : base(flow)
        {
            Processor = new ActionBlock<AudioDataMessage>(input =>
            {
                
            });
        }

        
    }
}