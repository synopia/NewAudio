using System;

namespace VL.NewAudio
{
    public class Decimator
    {
        public int Oversample { get; }
        private int quality;
        private float cutoff;

        private float[] inBuffer;
        private float[] kernel;
        private int inIndex;

        public Decimator(int oversample, int quality, float cutoff = 0.9f)
        {
            Oversample = oversample;
            this.quality = quality;
            this.cutoff = cutoff;
            kernel = new float[oversample * quality];
            inBuffer = new float[oversample * quality];
            BoxcarLowpassIR(kernel, cutoff * 0.5f / oversample);
            BlackmanHarrisWindow(kernel);
        }

        public void Reset()
        {
            inIndex = 0;
            Array.Clear(inBuffer, 0, inBuffer.Length);
        }

        public float Process(float[] input)
        {
            Array.Copy(input, 0, inBuffer, inIndex, Oversample);
            var length = inBuffer.Length;
            inIndex += Oversample;
            inIndex %= length;
            var output = 0.0f;
            for (int i = 0; i < length; i++)
            {
                var index = inIndex - 1 - i;
                index = (index + length) % length;
                output += kernel[i] * inBuffer[index];
            }

            return output;
        }


        public static void BoxcarLowpassIR(float[] output, float cutoff = 0.5f)
        {
            var len = output.Length;
            for (int i = 0; i < len; i++)
            {
                var t = i - (len - 1) / 2.0f;
                output[i] = 2 * cutoff * AudioEngine.SinC(2 * cutoff * t);
            }
        }

        private static float a0 = 0.35875f;
        private static float a1 = 0.48829f;
        private static float a2 = 0.14128f;
        private static float a3 = 0.01168f;

        public static void BlackmanHarrisWindow(float[] x)
        {
            var factor = 2 * AudioEngine.PI / (x.Length - 1);
            for (int i = 0; i < x.Length; i++)
            {
                x[i] *=
                    a0
                    - a1 * AudioEngine.CosF(1 * factor * i)
                    + a2 * AudioEngine.CosF(2 * factor * i)
                    - a3 * AudioEngine.CosF(3 * factor * i);
            }
        }
    }
}