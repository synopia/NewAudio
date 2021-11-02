using System;

namespace NewAudio.Internal
{
    public class ArrayAccessor
    {
        private float[] currentInputBuffer;
        private float[] currentOutputBuffer;

        public void Update(float[] outputBuffer, float[] inputBuffer)
        {
            currentInputBuffer = inputBuffer;
            currentOutputBuffer = outputBuffer;
        }

        public float[] GetValues()
        {
            return currentInputBuffer;
        }

        public void SetValues(float[] inp)
        {
            if (inp != null && inp.Length == currentOutputBuffer.Length)
                Array.Copy(inp, currentOutputBuffer, inp.Length);
        }
    }
}