using VL.NewAudio.Dsp;

namespace VL.NewAudio.Sources
{
    public struct AudioSourceChannelInfo
    {
        public AudioBuffer Buffer;
        public int StartFrame;
        public int NumFrames;

        public AudioSourceChannelInfo(AudioBuffer buffer, int startFrame, int numFrames)
        {
            Buffer = buffer;
            StartFrame = startFrame;
            NumFrames = numFrames;
        }

        public void ClearActiveBuffer()
        {
            Buffer?.Zero(StartFrame, NumFrames);
        }
    }
    public interface IAudioSource
    {
        void PrepareToPlay(int sampleRate, int framesPerBlockExpected);
        void ReleaseResources();
        void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill);
    }
}