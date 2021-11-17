using System;

namespace NewAudio.Core
{
    public readonly struct AudioTime
    {
        public bool Equals(AudioTime other)
        {
            return Time == other.Time;
        }

        public override bool Equals(object obj)
        {
            return obj is AudioTime other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Time;
        }

        public readonly int Time;
        public readonly double DTime;
        public readonly long RealTime;

        public AudioTime(int time, double dTime)
        {
            Time = time;
            DTime = dTime;
            RealTime = DateTime.Now.Ticks;
        }

        public static AudioTime operator +(AudioTime a, AudioTime b)
        {
            return new AudioTime(a.Time + b.Time, a.DTime + b.DTime);
        }

        public static AudioTime operator +(AudioTime a, AudioFormat f)
        {
            return new AudioTime(a.Time + f.NumberOfFrames, a.DTime + f.NumberOfFrames / (double)f.SampleRate);
        }

        public static bool operator ==(AudioTime a, AudioTime b)
        {
            return a.Time == b.Time;
        }

        public static bool operator !=(AudioTime a, AudioTime b)
        {
            return a.Time != b.Time;
        }

        public override string ToString()
        {
            return $"[{Time}, {DTime}, {RealTime}]";
        }
    }
}