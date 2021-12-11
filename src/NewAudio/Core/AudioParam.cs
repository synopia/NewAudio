using System;
using System.Collections.Generic;
using VL.NewAudio.Processor;

namespace VL.NewAudio.Core
{
    public record AudioEvent
    {
        public double TimeBegin { get; init; }
        public double TimeEnd { get; init; }
        public double TimeCancel { get; set; }
        public double Duration => TimeEnd - TimeBegin;
        public float? ValueBegin { get; init; }
        public float ValueEnd { get; init; }
        
        public bool IsCompleted { get; set; }
        public bool IsCanceled { get; set; }
        
    } 
    public class AudioParam
    {
        private object _lock;
        private List<AudioEvent> _events = new();
        public AudioPlayHead PlayHead { get; }
        public float Value { get; set; }

        public AudioParam(object @lock, AudioPlayHead playHead)
        {
            _lock = @lock;
            PlayHead = playHead;
        }

        public void ApplyRamp(float valueEnd, double durationSeconds)
        {
            double timeBegin = PlayHead.CurrentPosition.TimeSeconds;
            
            AudioEvent evt = new AudioEvent()
            {
                TimeBegin = timeBegin,
                TimeEnd = timeBegin + durationSeconds,
                ValueEnd = valueEnd
            };

            lock (_lock)
            {
                RemoveEvents(timeBegin);
                _events.Add(evt);    
            }
            
        }

        public void RemoveEvents(double time)
        {
            foreach (var evt in _events)
            {
                if (evt.TimeBegin >= time)
                {
                    evt.IsCanceled = true;
                } else if (evt.TimeEnd >= time)
                {
                    if (evt.TimeCancel > 0)
                    {
                        evt.TimeCancel = Math.Min(evt.TimeCancel, time);
                    }
                    else
                    {
                        evt.TimeCancel = time;
                    }
                }
            }
        }
    }
}