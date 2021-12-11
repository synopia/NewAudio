using System;
using Serilog;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Internal
{
    public class Decimator
    {
        private static readonly float a0 = 0.35875f;
        private static readonly float a1 = 0.48829f;
        private static readonly float a2 = 0.14128f;
        private static readonly float a3 = 0.01168f;
        private readonly ILogger _logger = Log.ForContext<Decimator>();
        private float cutoff;

        private readonly float[] inBuffer;
        private int inIndex;
        private readonly float[] kernel;
        private int quality;

        public Decimator(int oversample, int quality, float cutoff = 0.9f)
        {
            _logger.Information("Decimator: New Decimator created size={size}", oversample * quality);
            Oversample = oversample;
            this.quality = quality;
            this.cutoff = cutoff;
            kernel = new float[oversample * quality];
            inBuffer = new float[oversample * quality];
            BoxcarLowpassIR(kernel, cutoff * 0.5f / oversample);
            BlackmanHarrisWindow(kernel);
        }

        public int Oversample { get; }

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
            for (var i = 0; i < length; i++)
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
            for (var i = 0; i < len; i++)
            {
                var t = i - (len - 1) / 2.0f;
                output[i] = 2 * cutoff * AudioMath.SinC(2 * cutoff * t);
            }
        }

        public static void BlackmanHarrisWindow(float[] x)
        {
            var factor = 2 * AudioMath.Pi / (x.Length - 1);
            for (var i = 0; i < x.Length; i++)
            {
                x[i] *=
                    a0
                    - a1 * AudioMath.CosF(1 * factor * i)
                    + a2 * AudioMath.CosF(2 * factor * i)
                    - a3 * AudioMath.CosF(3 * factor * i);
            }
        }
    }
}