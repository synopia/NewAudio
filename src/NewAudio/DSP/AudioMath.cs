using System;
using System.Diagnostics;

namespace VL.NewAudio.Dsp
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
            return windowFunction switch
            {
                WindowFunction.Hamming => HammingWindow(size),
                WindowFunction.Hann => HannWindow(size),
                WindowFunction.BlackmanHarris => BlackmanHarrisWindow(size),
                WindowFunction.None=>FixedWindow(size, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(windowFunction), windowFunction, null)
            };
        }

        public static double[] FixedWindow(int size, double v)
        {
            double[] window = new double[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = v;
            }

            return window;
        }
        public static double[] BlackmanHarrisWindow(int size)
        {
            double[] window = new double[size];

            double alpha = 0.16;
            double a0 = 0.5 * (1 - alpha);
            double a1 = 0.5;
            double a2 = 0.5 * alpha;
            double oneOverN = 1.0 / ( size - 1 );
            
            for( int i = 0; i < size; i++ ) {
                double x = i * oneOverN;
                window[i] = a0 - a1 * Math.Cos( 2.0 * Math.PI * x ) + a2 * Math.Cos( 4.0 * Math.PI * x );
            }

            return window;
        }

        public static double[] HammingWindow(int size)
        {
            double[] window = new double[size];
            double alpha = 0.53836;
            double beta	= 1.0 - alpha;
            double oneOverN	= 1.0 / ( size - 1 );

            for( int i = 0; i < size; i++ ) {
                double x = i * oneOverN;
                window[i] = alpha - beta * Math.Cos( 2.0 * Math.PI * x );
            }

            return window;
        }

        public static double[] HannWindow(int size)
        {
            double[] window = new double[size];
            double alpha = 0.5;
            double oneOverN	= 1.0 / ( size - 1 );

            for( int i = 0; i < size; i++ ) {
                double x  = i * oneOverN;
                window[i] = alpha * ( 1.0 - Math.Cos( 2.0 * Math.PI * x ) );
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

        public static int Clamp(int x, int min, int max)
        {
            return x<min ? min: x>max ? max : x;
        }
        public static ulong Clamp(ulong x, ulong min, ulong max)
        {
            return x<min ? min: x>max ? max : x;
        }
        public static float Clamp(float x, float min, float max)
        {
            return x < min ? min : x > max ? max : x;
        }
        public static double ClampD(double x, double min, double max)
        {
            return x < min ? min : x > max ? max : x;
        }
        
        public static float Floor(float x)
        {
            return (float)Math.Floor(x);
        }
        
        public static float Fract(float x)
        {
            return x - Floor(x);
        }
        
        public static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        public static float TanH(float v)
        {
            return (float)Math.Tanh(v);
        }

        public static float SinF(float v)
        {
            return (float)Math.Sin(v);
        }

        public static float CosF(float v)
        {
            return (float)Math.Cos(v);
        }

        public static float SinH(float v)
        {
            return (float)Math.Sinh(v);
        }

        public static float SinC(float v)
        {
            if (Math.Abs(v) < 0.0000001f)
            {
                return 1.0f;
            }

            v *= Pi;
            return SinF(v) / v;
        }

        public static bool ThresholdBuffer(AudioBuffer buffer, float threshold, out int recordFrame)
        {
            var size = buffer.Size;
            for (int ch = 0; ch < buffer.NumberOfChannels; ch++)
            {
                for (int i = 0; i < size; i++)
                {
                    if (Math.Abs(buffer[ch, i]) > threshold)
                    {
                        recordFrame = i % buffer.NumberOfFrames;
                        return true;
                    }
                }
            }

            recordFrame = 0;
            return false;
        }
    }
}