using System;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Core
{
    public enum AudioStreamType
    {
        OnlyInput,
        OnlyOutput,
        Aggregate,
        FullDuplex,
        Mixed,
    }
    
    public interface IAudioStreamCallback
    {
        int OnBuffer(XtStream stream, in XtBuffer buffer, object user);

        void OnXRun(XtStream stream, int index, object user);

        void OnRunning(XtStream stream, bool running, ulong error, object user);
    }

    public interface IAudioStream : IDisposable
    {
        AudioStreamType Type { get; }
        int FramesPerBlock { get; }
        AudioStreamConfig Config { get; }
        void CreateBuffers(int numChannels, int numFrames);

    }

    public interface IAudioInputStream : IAudioStream
    {
        int NumInputChannels { get; }
        AudioBuffer? BindInput(XtBuffer buffer);
        double InputLatency { get; }
    }

    public interface IAudioOutputStream : IAudioStream
    {
        int NumOutputChannels { get; }
        AudioBuffer BindOutput(XtBuffer buffer);
        double OutputLatency { get; }

        void Start();
        void Stop();
        void Open(IAudioStreamCallback? callback);
    }
    
    public interface IAudioInputOutputStream: IAudioInputStream, IAudioOutputStream{}
}