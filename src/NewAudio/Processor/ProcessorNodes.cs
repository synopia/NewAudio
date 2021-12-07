using System.Collections;
using System.Collections.Generic;
using VL.Core;
using VL.NewAudio.Core;
using VL.NewAudio.Nodes;

namespace VL.NewAudio.Processor
{
    public static class ProcessorNodes
    {
        public static IEnumerable<IVLNodeDescription> GetNodeDescriptions(IVLNodeDescriptionFactory nodeFactory)
        {
            var category = "NewAudio.Processors";

            yield return nodeFactory.NewProcessorNode(_ => new AudioGraphIOProcessor(false), category: category,
                    name: "Input", hasAudioInput: false, hasAudioOutput: true, hasStateOutput: false)
                .WithEnabledPins();
            yield return nodeFactory.NewProcessorNode(_ => new AudioGraphIOProcessor(true), category: category,
                    name: "Output", hasAudioInput: true, hasAudioOutput: false, hasStateOutput: false)
                .WithEnabledPins();
            yield return nodeFactory.NewProcessorNode(_ => new NoiseGenProcessor(), category: category, name: "Noise",
                    hasAudioInput: false, hasAudioOutput: true, hasStateOutput: false)
                .WithEnabledPins();
            yield return nodeFactory.NewProcessorNode(_ => new SineGenProcessor(), category: category, name: "Sine",
                    hasAudioInput: false, hasAudioOutput: true, hasStateOutput: false)
                .WithEnabledPins()
                .AddInput(nameof(SineGenProcessor.Freq), x => x.Processor.Freq, (x, v) => x.Processor.Freq = v);

            yield return nodeFactory.NewProcessorNode(_ => new MultiplyProcessor(), category: category, name: "*",
                    hasAudioInput: true, hasAudioOutput: true, hasStateOutput: false)
                .WithEnabledPins()
                .AddInput(nameof(SineGenProcessor.Freq), x => x.Processor.Value, (x, v) => x.Processor.Value = v);


            yield return nodeFactory.NewNode(_ => new MonitorNode(), category: category, update: x => x.FillBuffer(),
                    name: "Monitor", hasStateOutput: false)
                .AddInput("Audio In", x => x.Input, (x, v) => x.Input = v)
                .AddInput(nameof(MonitorNode.BufferSize), x => x.BufferSize, (x, v) => x.BufferSize = v)
                .AddOutput(nameof(MonitorNode.Buffer), x => x.Buffer);
            yield return nodeFactory.NewNode(_ => new FftNode(true), category: category, update: x => x.FillBuffer(),
                    name: "FFT", hasStateOutput: false)
                .AddInput("Audio In", x => x.Input, (x, v) => x.Input = v)
                .AddInput(nameof(FftNode.FftSize), x => x.FftSize, (x, v) => x.FftSize = v)
                .AddOutput(nameof(FftNode.Buffer), x => x.Buffer);
            yield return nodeFactory.NewNode(_ => new FftNode(false), category: category, update: x => x.FillBuffer(),
                    name: "iFFT", hasStateOutput: false)
                .AddInput("Audio In", x => x.Input, (x, v) => x.Input = v)
                .AddInput(nameof(FftNode.FftSize), x => x.FftSize, (x, v) => x.FftSize = v)
                .AddOutput(nameof(FftNode.Buffer), x => x.Buffer);

        }
    }
}