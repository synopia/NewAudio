using System;

namespace VL.NewAudio
{
    public class AudioSampleAccessor
    {
        private float[] tempInputBuffer;

        private float[] currentInputBuffer;
        private float[] currentOutputBuffer;
        private int currentOutputChannels;
        private int currentInputChannels;
        private int currentIndex;

        public void Update(float[] outputBuffer, float[] inputBuffer, int outputChannels = 1, int inputChannels = 1)
        {
            currentInputBuffer = inputBuffer;
            currentOutputBuffer = outputBuffer;
            currentOutputChannels = outputChannels;

            if (inputChannels != currentInputChannels)
            {
                currentInputChannels = inputChannels;
                tempInputBuffer = new float[inputChannels];
            }
        }

        public void UpdateLoop(int inputIndex, int outputIndex)
        {
            if (currentInputBuffer != null)
            {
                Array.Copy(currentInputBuffer, inputIndex * currentInputChannels, tempInputBuffer, 0,
                    currentInputChannels);
            }

            currentIndex = outputIndex;
        }

        public float[] GetSamples()
        {
            return tempInputBuffer;
        }

        public void SetSamples(float[] inp)
        {
            if (inp != null)
            {
                for (int i = 0; i < currentOutputChannels; i++)
                {
                    currentOutputBuffer[currentIndex * currentOutputChannels + i] = inp[i % inp.Length];
                }
            }
        }
    }
}