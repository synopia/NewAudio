using System.Collections.Generic;
using VL.Core;
using VL.NewAudio.Core;
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
                .AddInput(nameof(AudioSourcePlayer.Gain), x => x.Gain, (x, v) => x.Gain = v);
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
            yield return nodeFactory.NewNode(_ => new AudioFileNode(), category: category, name: "AudioFile")
                .AddOutput(nameof(AudioFileNode.Source), x => x.Source)
                .AddInput(nameof(AudioFileNode.Path), x => x.Path, (x, v) => x.Path = v)
                .AddInput(nameof(AudioFileNode.Reset), x => false, (x, v) => x.Reset());
        }
    }
}