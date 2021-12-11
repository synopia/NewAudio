using System;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Core
{
    public interface IAudioDeviceCallback
    {
        void AudioDeviceCallback(AudioBuffer? input, AudioBuffer output, int numFrames);

        void AudioDeviceAboutToStart(IAudioSession session);
        void AudioDeviceStopped();

        void AudioDeviceError(string errorMessage);
    }

    public interface IAudioDevice : IDisposable
    {
        DeviceCaps Caps { get; }
        string Name { get; }
        string Id { get; }
        XtSystem System { get; }

        string[] OutputChannelNames { get; }
        string[] InputChannelNames { get; }
        int[] AvailableSampleRates { get; }
        XtSample[] AvailableSampleTypes { get; }
        (double, double) AvailableBufferSizes { get; }
        double DefaultBufferSize { get; }

        int NumAvailableInputChannels { get; }
        int NumAvailableOutputChannels { get; }

        bool HasControlPanel { get; }
        bool SupportsFullDuplex { get; }
        bool SupportsAggregation { get; }

        bool ShowControlPanel();

        XtSample ChooseBestSampleType(XtSample sample);
        int ChooseBestSampleRate(int rate);
        double ChooseBestBufferSize(double bufferSize);

        bool SupportsFormat(XtFormat format);
    }
}