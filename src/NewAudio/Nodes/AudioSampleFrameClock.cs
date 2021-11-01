using System;
using VL.Lib.Animation;

namespace NewAudio.Nodes
{
    public class AudioSampleFrameClock : IFrameClock
    {
        private Time _frameTime;

        public Time Time => _frameTime;
        public double TimeDifference { get; private set; }

        public void Init(double time)
        {
            _frameTime = time;
        }

        public void IncrementTime(double diff)
        {
            _frameTime += diff;
            TimeDifference = diff;
        }

        public IObservable<FrameTimeMessage> GetTicks()
        {
            throw new NotImplementedException();
        }

        public IObservable<FrameFinishedMessage> GetFrameFinished()
        {
            throw new NotImplementedException();
        }
    }
}