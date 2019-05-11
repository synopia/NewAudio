using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using NAudio.Wave;
using VL.Lib.Animation;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioSampleFrameClock : IFrameClock
    {
        private Time frameTime;
        private bool initialized;

        public Time Time => frameTime;
        public double TimeDifference { get; private set; }

        public void SetFrameTime(Time frameTime)
        {
            if (initialized)
            {
                TimeDifference = frameTime.Seconds - this.frameTime.Seconds;
            }
            else
            {
                TimeDifference = 0;
            }

            this.frameTime = frameTime;
            initialized = true;
        }

        public void IncrementTime(double diff)
        {
            frameTime += diff;
            TimeDifference = diff;
            initialized = true;
        }

        public void Restart()
        {
            initialized = false;
            frameTime = 0;
            TimeDifference = 0;
        }

        public IObservable<FrameTimeMessage> GetTicks()
        {
            throw new NotImplementedException();
        }
    }
    
    public class AudioSampleGenerateLoop<TState> where TState : class
    {
        private TState state;
        private readonly AudioSampleFrameClock sampleClock = new AudioSampleFrameClock();
        private readonly AudioSampleBuffer buffer = new AudioSampleBuffer();
        private Func<TState, Tuple<TState, float>> updateFunction;
        
        public ISampleProvider Update(
            bool reset,
            Func<IFrameClock, TState> create, Func<TState, Tuple<TState, float>> update )
        {
            if (reset || state == null)
            {
                state = create(sampleClock);
            }

            if (update != updateFunction)
            {
                buffer.Update = (b, offset, count) =>
                {
                    var increment = 1.0 / buffer.WaveFormat.SampleRate;
                    for (int i = 0; i < count; i++)
                    {
                        var result = update(state);
                        state = result.Item1;
                        b[i + offset] = result.Item2;
                        sampleClock.IncrementTime(increment);
                    }
                    
                };
                updateFunction = update;
            }

            return buffer;
        }
    }
}