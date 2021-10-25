using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NAudio.Wave;
using VL.Core;
using VL.Lib.Collections;
using FFTW.NET;

namespace NewAudio
{
    public static class Utils
    {
        public static float[] EnsureBuffer(float[] buffer, int bufferSize)
        {
            if (buffer == null || buffer.Length < bufferSize || buffer.Length > bufferSize * 2)
            {
                return new float[bufferSize];
            }

            return buffer;
        }
        
        public static float PI = (float)Math.PI;

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
            return (float) Math.Sinh(v);
        }

        public static float SinC(float v)
        {
            if (Math.Abs(v) < 0.0000001f)
                return 1.0f;
            v *= PI;
            return SinF(v) / v;
        }
        
        public static bool SequenceEquals<T>(IEnumerable<T> first, IEnumerable<T> second)
        {
            if (first == second)
                return true;
            if (first == null && second == null)
                return true;
            if (first == null || second == null)
                return false;
            return first.SequenceEqual(second);
        }

        public static bool ArrayEquals<T>(T[] first, T[] second)
        {
            if (first == second)
                return true;
            if (first == null && second == null)
                return true;
            if (first == null || second == null)
                return false;
            if (first.Length != second.Length)
                return false;
            for (var i = 0; i < first.Length; i++)
            {
                if (first[i]?.GetHashCode() != second[i]?.GetHashCode())
                    return false;
            }

            return true;
        }

    }

}