using System.Collections.Generic;
using VL.Core;
using VL.NewAudio.Core;

namespace VL.NewAudio.Device
{
    public static class SourceNodes
    {
        public static IEnumerable<IVLNodeDescription> GetNodeDescriptions(IVLNodeDescriptionFactory nodeFactory)
        {
            var category = "NewAudio.Sources";

            yield return nodeFactory.NewNode(_ => new GeneratorSource(), category: category, name: "Sine",
                    hasStateOutput: false)
                .AddInput(nameof(GeneratorSource.Frequency), x => x.Frequency, (x, v) => x.Frequency = v)
                .AddInput(nameof(GeneratorSource.Amplitude), x => x.Amplitude, (x, v) => x.Amplitude = v)
                .AddOutput(nameof(GeneratorSource.Output), x => x.Output);
            yield return nodeFactory.NewNode(_ => new AudioSourcePlayer(), category: category, name: "Player",
                    hasStateOutput: true)
                .AddInput(nameof(AudioSourcePlayer.Input), x => x.Input, (x, v) => x.Input = v)
                .AddInput(nameof(AudioSourcePlayer.Gain), x => x.Gain, (x, v) => x.Gain = v);
            yield return nodeFactory.NewNode(_ => new AudioProcessorPlayer(), category: category,
                    name: "ProcessorPlayer",
                    hasStateOutput: true)
                .AddInput(nameof(AudioProcessorPlayer.Processor), x => x.Processor, (x, v) => x.Processor = v);
                // .AddInput(nameof(AudioSourcePlayer.Gain), x => x.Gain, (x, v) => x.Gain = v);
            yield return nodeFactory.NewNode(_ => new ChannelRouterSource(), category: category, name: "Router",
                    hasStateOutput: false)
                .AddInput(nameof(ChannelRouterSource.Input), x => x.Input, (x, v) => x.Input = v)
                .AddListInput(nameof(ChannelRouterSource.InputMap), x => x.InputMap, (x, v) => x.InputMap = v)
                .AddListInput(nameof(ChannelRouterSource.OutputMap), x => x.OutputMap, (x, v) => x.OutputMap = v)
                .AddOutput(nameof(GeneratorSource.Output), x => x.Output);
            yield return nodeFactory.NewNode(_ => new MixerSource(), category: category, name: "Mixer",
                    hasStateOutput: false)
                .AddInput(nameof(MixerSource.Inputs), x => x.Inputs, (x, v) => x.Inputs = v)
                .AddOutput(nameof(GeneratorSource.Output), x => x.Output);
        }
    }
}