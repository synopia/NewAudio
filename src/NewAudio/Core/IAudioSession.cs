﻿using System;
using System.Collections.Generic;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Core
{
    public interface IAudioSession : IDisposable
    {
        void Start();
        void Stop();

        int CurrentSampleRate { get; }
        int CurrentFramesPerBlock { get; }
        AudioChannels ActiveInputChannels { get; }
        AudioChannels ActiveOutputChannels { get; }

        int XRuns { get; }
        double CpuUsage { get; }
        AudioStreamType Type { get; }
        double InputLatency { get; }
        double OutputLatency { get; }
        IEnumerable<string> Times { get; }
    }
}