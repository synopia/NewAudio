using System;
using NAudio.Dsp;

namespace NewAudio.Internal
{

    public static class FFTUtils
    {
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

        public static double[] CreateWindow(WindowFunction windowFunction, int size)
        {
            Func<int, int, double> function = (i, n) => 1;;
            switch (windowFunction)
            {
                case WindowFunction.Hamming:
                    function = FastFourierTransform.HammingWindow;
                    break;
                case WindowFunction.Hann:
                    function = FastFourierTransform.HannWindow;
                    break;
                case WindowFunction.BlackmanHarris:
                    function = FastFourierTransform.BlackmannHarrisWindow;
                    break;
            }

            double[] window = new double[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = function(i, size);
            }

            return window;
        }

    }
}