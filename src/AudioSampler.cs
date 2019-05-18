using System.Collections.Generic;

namespace VL.NewAudio
{
    public class AudioSampler
    {
        private float Input;
        private AudioSampleBuffer output;
        private AudioSamplerProcessor processor = new AudioSamplerProcessor();

        public AudioSampleBuffer Update(float input)
        {
            Input = input;

            processor.Input = input;
            if (output == null)
            {
                output = new AudioSampleBuffer(WaveOutput.SingleChannelFormat)
                {
                    Processor = processor
                };
            }

            return output;
        }

        private class AudioSamplerProcessor : IAudioProcessor
        {
            public float Input;

            private float lastInput;

            public List<AudioSampleBuffer> GetInputs()
            {
                return AudioSampleBuffer.EmptyList;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                var d = (Input - lastInput) / count;
                for (int i = 0; i < count; i++)
                {
                    buffer[i + offset] = lastInput + d * i;
                }

                lastInput = Input;
                return count;
            }
        }
    }
}