using System.Linq;
using NAudio.Wave.SampleProviders;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioMixer : BaseAudioNode
    {
        public AudioSampleBuffer[] Inputs;
        public int[] OutputMap;
        public bool HasChanges;
        private AudioSampleBuffer output;
        private AudioMixerProcessor processor;

        public AudioSampleBuffer Update(Spread<AudioSampleBuffer> inputs, Spread<int> outputMap)
        {
            var arrayInputs = inputs?.ToArray();
            var arrayMap = outputMap?.ToArray();
            HasChanges = !AudioEngine.ArrayEquals(Inputs, arrayInputs) ||
                         !AudioEngine.ArrayEquals(OutputMap, arrayMap);

            Inputs = arrayInputs;
            OutputMap = arrayMap;

            if (HasChanges || HotSwapped)
            {
                AudioEngine.Log($"AudioMixer: configuration changed");

                if (IsValid())
                {
                    processor = new AudioMixerProcessor(Inputs, OutputMap);
                    output = processor.Build();
                }
                else
                {
                    output?.Dispose();
                    output = null;
                }

                HotSwapped = false;
            }

            return output;
        }

        private bool IsValid()
        {
            return Inputs != null && Inputs.Length > 0;
        }

        private class AudioMixerProcessor : IAudioProcessor
        {
            private readonly AudioSampleBuffer[] inputs;
            private int[] outputMap;
            private MultiplexingSampleProvider multiplexer;

            public AudioMixerProcessor(AudioSampleBuffer[] inputs, int[] outputMap)
            {
                this.inputs = inputs;
                this.outputMap = outputMap;
                this.inputs = inputs.Select(input =>
                {
                    if (input != null)
                    {
                        return input;
                    }

                    return AudioSampleBuffer.Silence();
                }).ToArray();
            }

            public int Read(float[] buffer, int offset, int count)
            {
                return multiplexer.Read(buffer, offset, count);
            }

            public AudioSampleBuffer Build()
            {
                BuildOutputMap();

                multiplexer = new MultiplexingSampleProvider(inputs, outputMap.Length);
                for (int i = 0; i < outputMap.Length; i++)
                {
                    var input = outputMap[i];
                    multiplexer.ConnectInputToOutput(input, i);
                    AudioEngine.Log($" ch: {input} ==> {i}");
                }

                return new AudioSampleBuffer(multiplexer.WaveFormat) {Processor = this};
            }

            private void BuildOutputMap()
            {
                if (outputMap == null || outputMap.Length == 0)
                {
                    var totalChannels = inputs.Select(b => b.WaveFormat.Channels).Sum();
                    outputMap = new int[totalChannels];
                    for (var i = 0; i < totalChannels; i++)
                        outputMap[i] = i;
                }
            }
        }
    }
}