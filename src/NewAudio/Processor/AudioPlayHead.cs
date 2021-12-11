namespace VL.NewAudio.Processor
{
    public class AudioPlayHead
    {
        public struct PositionInfo
        {
            public ulong SampleTime;
            public double TimeSeconds;
            public bool IsPlaying;
            public bool IsRecording;
            public bool IsLooping;
        }

        public PositionInfo CurrentPosition { get; }
    }
}