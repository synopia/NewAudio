using System.Collections.Generic;
using VL.Core;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Nodes;

namespace VL.NewAudio.Sources
{
    public static class SourceNodes
    {
        public static IEnumerable<IVLNodeDescription> GetNodeDescriptions(IVLNodeDescriptionFactory nodeFactory)
        {
            var category = "NewAudio.Sources";

            yield return nodeFactory.NewNode(_ => new GeneratorSource(), category: category, name: "Sine",
                    hasStateOutput: true)
                .AddInput(nameof(GeneratorSource.Frequency), x => x.Frequency, (x, v) => x.Frequency = v)
                .AddInput(nameof(GeneratorSource.Amplitude), x => x.Amplitude, (x, v) => x.Amplitude = v);
            yield return nodeFactory.NewNode(_ => new AudioSourcePlayer(), category: category, name: "Player",
                    hasStateOutput: true)
                .AddInput(nameof(AudioSourcePlayer.Source), x => x.Source, (x, v) => x.Source = v)
                .AddInput(nameof(AudioSourcePlayer.Gain), x => x.Gain, (x, v) => x.Gain = v)
                .AddInput(nameof(AudioSourcePlayer.GGain), x => x.GGain, (x, v) => x.GGain = v);
            yield return nodeFactory.NewNode(_ => new AudioProcessorPlayer(), category: category,
                    name: "ProcessorPlayer",
                    hasStateOutput: true)
                .AddInput(nameof(AudioProcessorPlayer.Processor), x => x.Processor, (x, v) => x.Processor = v);
            yield return nodeFactory.NewNode(_ => new ChannelRouterSource(), category: category, name: "Router",
                    hasStateOutput: true)
                .AddInput(nameof(ChannelRouterSource.Source), x => x.Source, (x, v) => x.Source = v)
                .AddListInput(nameof(ChannelRouterSource.InputMap), x => x.InputMap, (x, v) => x.InputMap = v)
                .AddListInput(nameof(ChannelRouterSource.OutputMap), x => x.OutputMap, (x, v) => x.OutputMap = v);
            yield return nodeFactory.NewNode(_ => new MixerSource(), category: category, name: "Mixer",
                    hasStateOutput: true)
                .AddInput(nameof(MixerSource.Sources), x => x.Sources, (x, v) => x.Sources = v);
            yield return nodeFactory.NewNode(_ => new AudioTransportSource(), category: category, name: "Buffer",
                    hasStateOutput: true)
                .AddInput(nameof(AudioTransportSource.Source), x => x.Source, (x, v) => x.Source = v)
                .AddInput(nameof(AudioTransportSource.ReadAhead), x => x.ReadAhead, (x, v) => x.ReadAhead = v)
                .AddInput(nameof(AudioTransportSource.SourceSampleRate), x => x.SourceSampleRate, (x, v) => x.SourceSampleRate = v)
                .AddInput(nameof(AudioTransportSource.Gain), x => x.Gain, (x, v) => x.Gain = v, 1.0f)
                .AddInput(nameof(AudioTransportSource.Start), x => x.IsPlaying, (x, v) =>
                {
                    if (v)
                    {
                        x.Start();
                    }
                })
                .AddInput(nameof(AudioTransportSource.Stop), x => !x.IsPlaying, (x, v) =>
                {
                    if (v)
                    {
                        x.Stop();
                    }
                })
                .AddOutput(nameof(AudioTransportSource.Position), x => x.Position)
                .AddOutput(nameof(AudioTransportSource.LengthInSeconds), x => x.LengthInSeconds)
                .AddOutput(nameof(AudioTransportSource.IsPlaying), x => x.IsPlaying)
                .AddOutput(nameof(AudioTransportSource.IsLooping), x => x.IsLooping);
            yield return nodeFactory.NewNode(_ => new AudioFileNode(), category: category, name: "AudioFile", update:x=>x.Update())
                .AddOutput(nameof(AudioFileNode.Source), x => x.Source)
                .AddOutput(nameof(AudioFileNode.SampleRate), x => x.SampleRate)
                .AddInput(nameof(AudioFileNode.IsLooping), x=>x.IsLooping, (x,v)=>x.IsLooping=v, false)
                .AddInput(nameof(AudioFileNode.Path), x => x.Path, (x, v) => x.Path = v);
            yield return nodeFactory.NewNode(_ => new AudioBufferOutSource(), category: category,
                    name: "AudioBufferOut", hasStateOutput: true)
                .AddInput(nameof(AudioBufferOutSource.Source), x => x.Source, (x, v) => x.Source = v)
                .AddInput(nameof(AudioBufferOutSource.BufferSize), x => x.BufferSize, (x, v) => x.BufferSize = v)
                .AddOutput(nameof(AudioBufferOutSource.Buffer), x =>
                {
                    x.FillBuffer();
                    return x.Buffer;
                });
            yield return nodeFactory.NewNode(_ => new FFTSource(true), category: category,
                    name: "FFT", hasStateOutput: true)
                .AddInput(nameof(FFTSource.Source), x => x.Source, (x, v) => x.Source = v)
                .AddInput(nameof(FFTSource.FftSize), x => x.FftSize, (x, v) => x.FftSize = v)
                .AddInput(nameof(FFTSource.WindowFunction), x => x.WindowFunction, (x, v) => x.WindowFunction = v)
                .AddOutput(nameof(FFTSource.Buffer), x =>
                {
                    x.FillBuffer();
                    return x.Buffer;
                });
            yield return nodeFactory.NewNode(_ => new FFTSource(false), category: category,
                    name: "iFFT", hasStateOutput: true)
                .AddInput(nameof(FFTSource.Source), x => x.Source, (x, v) => x.Source = v)
                .AddInput(nameof(FFTSource.FftSize), x => x.FftSize, (x, v) => x.FftSize = v)
                .AddInput(nameof(FFTSource.WindowFunction), x => x.WindowFunction, (x, v) => x.WindowFunction = v)
                .AddOutput(nameof(FFTSource.Buffer), x =>
                {
                    x.FillBuffer();
                    return x.Buffer;
                });
        }
    }
}