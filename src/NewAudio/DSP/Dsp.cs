using System;

namespace VL.NewAudio.Dsp
{
    public static class Dsp
    {
        public static void CopyFrom(this Span<float> target, Span<float> source, int length)
        {
            source.Slice(0, length).CopyTo(target);
        }

        public static void Fill(this Span<float> target, float value, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = value;
            }
        }

        public static float Sum(Span<float> array, int length)
        {
            var result = 0.0f;
            for (var i = 0; i < length; i++)
            {
                result += array[i];
            }

            return result;
        }

        public static void Add(Span<float> array, float scalar, Span<float> target, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = array[i] + scalar;
            }
        }

        public static void Add(this Span<float> target, Span<float> source, int length)
        {
            Add(source, target, target, length);
        }

        public static void Add(Span<float> arrayOne, Span<float> arrayTwo, Span<float> target, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = arrayOne[i] + arrayTwo[i];
            }
        }

        public static void Sub(Span<float> array, float scalar, Span<float> target, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = array[i] - scalar;
            }
        }

        public static void Sub(Span<float> arrayOne, Span<float> arrayTwo, Span<float> target, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = arrayOne[i] - arrayTwo[i];
            }
        }

        public static float Rms(Span<float> array, int length)
        {
            var result = 0.0f;
            for (var i = 0; i < length; i++)
            {
                var value = array[i];
                result += value * value;
            }

            return result;
        }

        public static void Mul(Span<float> array, float scalar, Span<float> target, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = array[i] * scalar;
            }
        }

        public static void Mul(Span<float> arrayOne, Span<float> arrayTwo, Span<float> target, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = arrayOne[i] * arrayTwo[i];
            }
        }

        public static void Div(Span<float> array, float scalar, Span<float> target, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = array[i] / scalar;
            }
        }

        public static void Div(Span<float> arrayOne, Span<float> arrayTwo, Span<float> target, int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = arrayOne[i] / arrayTwo[i];
            }
        }

        public static void AddMul(Span<float> arrayOne, Span<float> arrayTwo, float scalar, Span<float> target,
            int length)
        {
            for (var i = 0; i < length; i++)
            {
                target[i] = (arrayOne[i] + arrayTwo[i]) * scalar;
            }
        }

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
                Mul(array, maxValue / max, array, length);
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