using System;
using NAudio.Dsp;

namespace NewAudio.Internal
{

    public static class FFTUtils
    {
        public enum WindowFunction
        {
            None,
            Hamming,
            Hann,
            BlackmanHarris
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