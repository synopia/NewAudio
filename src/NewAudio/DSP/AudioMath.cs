using System;
using NAudio.Dsp;
using SharedMemory;

namespace NewAudio.Dsp
{
    public static class AudioMath
    {
        public const float Pi = (float)System.Math.PI;
        public const float TwoPi = (float)(2.0 * System.Math.PI);
        public const double Epsilon = 4.37114e-05;
        public enum WindowFunction
        {
            None,
            Hamming,
            Hann,
            BlackmanHarris
        }

        public static double[] CreateWindow(WindowFunction windowFunction, int size)
        {
            Func<int, int, double> function = windowFunction switch
            {
                WindowFunction.Hamming => FastFourierTransform.HammingWindow,
                WindowFunction.Hann => FastFourierTransform.HannWindow,
                WindowFunction.BlackmanHarris => FastFourierTransform.BlackmannHarrisWindow,
                _ => (_, _) => 1
            };

            var window = new double[size];
            for (var i = 0; i < size; i++)
            {
                window[i] = function(i, size);
            }

            return window;
        }

        public static uint UpperPow2(uint v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        public static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        public static float TanH(float v)
        {
            return (float)System.Math.Tanh(v);
        }

        public static float SinF(float v)
        {
            return (float)System.Math.Sin(v);
        }

        public static float CosF(float v)
        {
            return (float)System.Math.Cos(v);
        }

        public static float SinH(float v)
        {
            return (float)System.Math.Sinh(v);
        }

        public static float SinC(float v)
        {
            if (System.Math.Abs(v) < 0.0000001f)
            {
                return 1.0f;
            }

            v *= Pi;
            return SinF(v) / v;
        }
    }
}