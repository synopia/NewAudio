using VL.NewAudio.Dsp;

namespace VL.NewAudio.Core
{
    public readonly struct AudioBufferToFill
    {
        public readonly AudioBuffer Buffer;
        public readonly int StartFrame;
        public readonly int NumFrames;

        public AudioBufferToFill(AudioBuffer buffer, int startFrame, int numFrames)
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
        void FillNextBuffer(AudioBufferToFill buffer);
    }
}