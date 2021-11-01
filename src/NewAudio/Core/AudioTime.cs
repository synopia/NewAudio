namespace NewAudio.Core
{
    public readonly struct AudioTime
    {
        public readonly int Time;
        public readonly double DTime;

        public AudioTime(int time, double dTime)
        {
            Time = time;
            DTime = dTime;
        }

        public static AudioTime operator +(AudioTime a, AudioTime b) =>
            new AudioTime(a.Time + b.Time, a.DTime + b.DTime);
        public static AudioTime operator +(AudioTime a, AudioFormat f) =>
            new AudioTime(a.Time + f.SampleCount, a.DTime + f.SampleCount/(double)f.SampleRate);

        public static bool operator ==(AudioTime a, AudioTime b) => a.Time == b.Time;
        public static bool operator !=(AudioTime a, AudioTime b) => a.Time != b.Time;

        public override string ToString()
        {
            return $"[{Time}, {DTime}]";
        }
    }
}