using System;

namespace VL.NewAudio.Dsp
{
    public static class Dsp
    {

        public static void Normalize(Span<float> array, int length, float maxValue)
        {
            var max = 0.0f;
            for (var i = 0; i < length; i++)
            {
                if (array[i] > max)
                {
                    max = array[i];
                }
            }

            if (max > 0.00001f)
            {
                // Mul(array, maxValue / max, array, length);
            }
        }

        public static float SpectralCentroid(Span<float> array, int length, int sampleRate)
        {
            var binToFreq = (float)sampleRate / (float)(length * 2);
            float FA = 0;
            float A = 0;
            for (var i = 0; i < length; i++)
            {
                var freq = i * binToFreq;
                var mag = array[i];
                FA += freq * mag;
                A += mag;
            }

            if (A < AudioMath.Epsilon)
            {
                return 0;
            }

            return FA / A;
        }
    }
}