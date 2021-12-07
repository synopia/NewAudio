using System.Collections.Generic;
using VL.Core;
using VL.NewAudio.Device;
using VL.NewAudio.Nodes;

namespace VL.NewAudio.Core
{
    public static class CoreNodes
    {
        public static IEnumerable<IVLNodeDescription> GetNodeDescriptions(IVLNodeDescriptionFactory nodeFactory)
        {
            var category = "NewAudio.Devices";

            yield return nodeFactory.NewAudioNode(ctor: ctx => new AudioDeviceNode(), category: category,
                    name: "AudioDevice", hasStateOutput: false)
                .AddInput(nameof(AudioDeviceNode.Device), x => x.Device, (x, v) => x.Device = v, defaultValue: default)
                .AddOutput(nameof(AudioDeviceNode.Name), x => x.Name)
                .AddOutput(nameof(AudioDeviceNode.System), x => x.System)
                .AddOutput(nameof(AudioDeviceNode.AvailableInputChannels), x => x.AvailableInputChannels)
                .AddOutput(nameof(AudioDeviceNode.AvailableOutputChannels), x => x.AvailableOutputChannels)
                .AddOutput(nameof(AudioDeviceNode.AvailableSampleFrequencies), x => x.AvailableSampleFrequencies)
                .AddOutput(nameof(AudioDeviceNode.AvailableSampleTypes), x => x.AvailableSampleTypes)
                .AddOutput(nameof(AudioDeviceNode.MinBufferSizeMs), x => x.MinBufferSizeMs)
                .AddOutput(nameof(AudioDeviceNode.MaxBufferSizeMs), x => x.MaxBufferSizeMs)
                .AddOutput(nameof(AudioDeviceNode.InputChannelNames), x => x.InputChannelNames)
                .AddOutput(nameof(AudioDeviceNode.OutputChannelNames), x => x.OutputChannelNames)
                .AddOutput(nameof(AudioDeviceNode.AudioDevice), x => x.AudioDevice);

            yield return nodeFactory.NewAudioNode(_ => new AudioStreamNode(), category: category, name: "AudioStream",
                    hasStateOutput: false)
                .AddInput(nameof(AudioStreamNode.AudioDevice), x => x.AudioDevice, (x, v) => x.AudioDevice = v)
                .AddListInput(nameof(AudioStreamNode.InputChannels), x => x.InputChannels,
                    (x, v) => x.InputChannels = v)
                .AddListInput(nameof(AudioStreamNode.OutputChannels), x => x.OutputChannels,
                    (x, v) => x.OutputChannels = v)
                .AddInput(nameof(AudioStreamNode.SamplingFrequency), x => x.SamplingFrequency,
                    (x, v) => x.SamplingFrequency = v, defaultValue: SamplingFrequency.Hz44100)
                .AddInput(nameof(AudioStreamNode.BufferSize), x => x.BufferSize, (x, v) => x.BufferSize = v,
                    defaultValue: 0)
                .AddOutput(nameof(AudioStreamNode.Config), x => x.Config);

            yield return nodeFactory.NewAudioNode(_ => new AudioSessionNode(), category: category, name: "AudioSession",
                    hasStateOutput: false, update: (x) => x.Update())
                .WithEnabledPins()
                .AddInput(nameof(AudioSessionNode.Input), x => x.Input, (x, v) => x.Input = v)
                .AddInput(nameof(AudioSessionNode.Primary), x => x.Primary, (x, v) => x.Primary = v)
                .AddListInput(nameof(AudioSessionNode.Secondary), x => x.Secondary, (x, v) => x.Secondary = v)
                .AddOutput(nameof(AudioSessionNode.InputLatency), x => x.InputLatency)
                .AddOutput(nameof(AudioSessionNode.OutputLatency), x => x.OutputLatency)
                .AddOutput(nameof(AudioSessionNode.XRuns), x => x.XRuns)
                .AddOutput(nameof(AudioSessionNode.Type), x => x.Type);

        }
    }
}