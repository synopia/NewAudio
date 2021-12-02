using System;
using NewAudio.Dsp;
using Xt;

namespace NewAudio.Device
{
    public enum AudioStreamType
    {
        OnlyInput,
        OnlyOutput,
        MixedSystems,
        MixedDevices,
        FullDuplex
    }

    public readonly struct AudioStreamConfig
    {
        public XtDevice Device { get; init; }
        public XtSystem System { get; init; }
        public AudioChannels ActiveChannels { get; init; }
        public int SampleRate { get; init; }
        public XtSample SampleType { get; init; }
        public bool Interleaved { get; init; }
        public double BufferSize { get; init; }


        private (AudioStreamType, IAudioStream, IAudioStream?) Factory<TSampleType, TMemoryAccess>(AudioStreamConfig? inputConfig, IAudioStreamCallback renderCallback, IAudioStreamCallback? captureCallback)
            where TSampleType : struct, ISampleType
            where TMemoryAccess : struct, IMemoryAccess
        {
            if (inputConfig == null)
            {
                return (AudioStreamType.OnlyOutput, new AudioOutputStream<TSampleType, TMemoryAccess>(Device, ActiveChannels, SampleRate,
                    BufferSize, renderCallback), null);
            }

            var i = inputConfig.Value;
            if (i.Device == Device)
            {
                var input = new AudioInputStream<TSampleType, TMemoryAccess>(i.Device, i.ActiveChannels, i.SampleRate,
                    i.BufferSize, null);
                return (AudioStreamType.FullDuplex, new AudioFullDuplexStream<TSampleType, TMemoryAccess>(input, ActiveChannels, SampleRate,
                    BufferSize, renderCallback), input);
            }
            var input2 = new AudioInputStream<TSampleType, TMemoryAccess>(i.Device, i.ActiveChannels, i.SampleRate,
                i.BufferSize, captureCallback);

            var type = i.System == System ? AudioStreamType.MixedDevices : AudioStreamType.MixedSystems; 
            return (type, new AudioOutputStream<TSampleType, TMemoryAccess>( Device,ActiveChannels, SampleRate, BufferSize, renderCallback), input2);
        }

        public (AudioStreamType, IAudioStream, IAudioStream?) CreateStream(AudioStreamConfig? inputConfig, IAudioStreamCallback renderCallback, IAudioStreamCallback? captureCallback)
        {
            if (Interleaved)
            {
                return SampleType switch
                {
                    XtSample.Float32 => Factory<Float32Sample, Interleaved>(inputConfig, renderCallback, captureCallback),
                    XtSample.Int32 => Factory<Int32LsbSample, Interleaved>(inputConfig, renderCallback, captureCallback),
                    XtSample.Int24 => Factory<Int24LsbSample, Interleaved>(inputConfig, renderCallback, captureCallback),
                    XtSample.Int16 => Factory<Int16LsbSample, Interleaved>(inputConfig, renderCallback, captureCallback),
                    _ => throw new ArgumentOutOfRangeException(nameof(SampleType), SampleType, null)
                };
            }
            else
            {
                return SampleType switch
                {
                    XtSample.Float32 => Factory<Float32Sample, NonInterleaved>(inputConfig, renderCallback, captureCallback),
                    XtSample.Int32 => Factory<Int32LsbSample, NonInterleaved>(inputConfig, renderCallback, captureCallback),
                    XtSample.Int24 => Factory<Int24LsbSample, NonInterleaved>(inputConfig, renderCallback, captureCallback),
                    XtSample.Int16 => Factory<Int16LsbSample, NonInterleaved>(inputConfig, renderCallback, captureCallback),
                    _ => throw new ArgumentOutOfRangeException(nameof(SampleType), SampleType, null)
                };
            }
        }
    }
}