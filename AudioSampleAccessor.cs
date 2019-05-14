using System;

namespace VL.NewAudio
{
    public class AudioSampleAccessor
    {
        private float[] tempBuffer;

        public float[] GetSamples(float[] buffer, int index, int channels = 1)
        {
            if (tempBuffer == null || channels != tempBuffer.Length)
            {
                tempBuffer = new float[channels];
            }

            if (buffer != null && index + channels <= buffer.Length)
            {
                Array.Copy(buffer, index, tempBuffer, 0, channels);
            }

            return tempBuffer;
        }
    }
}