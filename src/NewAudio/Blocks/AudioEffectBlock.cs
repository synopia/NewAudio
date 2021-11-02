using NewAudio.Core;

namespace NewAudio.Blocks
{
    public class AudioEffectBlock : BaseAudioBlock<AudioDataMessage, AudioDataMessage>
    {
        public AudioEffectBlock(AudioDataflow flow) : base(flow)
        {
            // Processor = new ActionBlock<AudioDataMessage>(input =>
            // {

            // });
        }
    }
}