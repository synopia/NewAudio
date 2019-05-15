using System.Linq;
using NAudio.Wave.SampleProviders;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioMixer
    {
        private struct Configuration
        {
            public AudioSampleBuffer[] Inputs;
            public int[] OutputMap;
            public bool HasChanges;

            public void Update(Spread<AudioSampleBuffer> inputs, Spread<int> outputMap)
            {
                var arrayInputs = inputs?.ToArray();
                var arrayMap = outputMap?.ToArray();
                HasChanges = !AudioEngine.ArrayEquals(Inputs, arrayInputs) ||
                             !AudioEngine.ArrayEquals(OutputMap, arrayMap);

                Inputs = arrayInputs;
                OutputMap = arrayMap;
            }

            public bool IsValid()
            {
                return Inputs != null && Inputs.Length > 0;
            }
        }

        private Configuration config = new Configuration();
        private MultiplexingSampleProvider multiplexer;
        private AudioSampleBuffer output;

        public AudioSampleBuffer Update(Spread<AudioSampleBuffer> inputs, Spread<int> outputMap)
        {
            config.Update(inputs, outputMap);

            if (config.HasChanges)
            {
                AudioEngine.Log($"AudioMixer: configuration changed");

                if (config.IsValid())
                {
                    Build();
                }
                else
                {
                    output?.Dispose();
                    output = null;
                }
            }

            return output;
        }

        private void Build()
        {
            var list = config.Inputs.Select(input =>
            {
                if (input != null)
                {
                    return input;
                }

                return AudioSampleBuffer.Silence();
            }).ToArray();

            var outputMap = GetOutputMap(list);

            multiplexer = new MultiplexingSampleProvider(list, outputMap.Length);
            for (int i = 0; i < outputMap.Length; i++)
            {
                var input = outputMap[i];
                multiplexer.ConnectInputToOutput(input, i);
                AudioEngine.Log($" ch: {input} ==> {i}");
            }

            output = new AudioSampleBuffer(multiplexer.WaveFormat);
            output.Update = (b, o, c) => multiplexer.Read(b, o, c);
        }

        private int[] GetOutputMap(AudioSampleBuffer[] list)
        {
            var outputMap = config.OutputMap;
            if (outputMap == null || outputMap.Length == 0)
            {
                var totalChannels = list.Select(b => b.WaveFormat.Channels).Sum();
                outputMap = new int[totalChannels];
                for (var i = 0; i < totalChannels; i++)
                    outputMap[i] = i;
            }

            return outputMap;
        }
    }
}