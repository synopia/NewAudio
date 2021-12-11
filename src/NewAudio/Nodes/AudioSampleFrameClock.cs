using System;
using VL.Lib.Animation;

namespace VL.NewAudio.Nodes
{
    public class AudioSampleFrameClock : IFrameClock
    {
        public Time Time { get; private set; }

        public double TimeDifference { get; private set; }

        public IObservable<FrameTimeMessage> GetTicks()
        {
            throw new NotImplementedException();
        }

        public IObservable<FrameFinishedMessage> GetFrameFinished()
        {
            throw new NotImplementedException();
        }

        public void Init(double time)
        {
            Time = time;
        }

        public void IncrementTime(double diff)
        {
            Time += diff;
            TimeDifference = diff;
        }
    }
}