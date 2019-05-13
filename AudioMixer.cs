using System.Linq;
using NAudio.Wave.SampleProviders;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioMixer
    {
        private AudioSampleBuffer[] inputs = new AudioSampleBuffer[0];
        private int[] outputMap = new int[0];

        private MultiplexingSampleProvider multiplexer;
        private AudioSampleBuffer output;

        public AudioSampleBuffer Mixer(Spread<AudioSampleBuffer> inputs, Spread<int> outputMap)
        {
            if (inputs == null || outputMap == null)
            {
                return null;
            }

            if (HasChanged(inputs, outputMap))
            {
                this.inputs = new AudioSampleBuffer[inputs.Count];
                inputs.CopyTo(0, this.inputs, 0, inputs.Count);
                this.outputMap = new int[outputMap.Count];
                outputMap.CopyTo(0, this.outputMap, 0, outputMap.Count);

                Build();
            }

            return output;
        }

        private bool HasChanged(Spread<AudioSampleBuffer> inputs, Spread<int> outputMap)
        {
            if (inputs.Count != this.inputs.Length)
                return true;
            if (outputMap.Count != this.outputMap.Length)
                return true;
            if (this.inputs.Where((t, i) => inputs[i] != t).Any())
            {
                return true;
            }

            if (this.outputMap.Where((t, i) => outputMap[i] != t).Any())
            {
                return true;
            }

            return false;
        }

        private void Build()
        {
            AudioEngine.Log($"AudioMixer: configuration changed");
            multiplexer = new MultiplexingSampleProvider(inputs, outputMap.Length);
            for (int i = 0; i < outputMap.Length; i++)
            {
                var input = outputMap[i];
                multiplexer.ConnectInputToOutput(input, i);
            }

            output = new AudioSampleBuffer(multiplexer.WaveFormat);
            output.Update = (b, o, c) => { multiplexer.Read(b, o, c); };
        }
    }
}