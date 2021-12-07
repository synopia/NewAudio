using System;
using System.Buffers;
using VL.NewAudio.Device;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Device
{
    public interface IAudioSession: IDisposable
    {
        void Start();
        void Stop();
        
        int CurrentSampleRate { get; }
        int CurrentFramesPerBlock { get;  }
        AudioChannels ActiveInputChannels { get; }
        AudioChannels ActiveOutputChannels { get; }
        
        int XRuns { get; }
        AudioStreamType Type { get; }
        double InputLatency { get; }
        double OutputLatency { get; }
    }
}