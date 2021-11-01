using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NAudio.Wave;
using VL.Core;
using VL.Lib.Collections;
using FFTW.NET;
using SharedMemory;

namespace NewAudio.Nodes
{
    public static class Utils
    {
        
        public static string CalculateBufferStats(CircularBuffer buffer)
        {
            var header = buffer.ReadNodeHeader();
            var nodes = header.NodeCount;
            var ws = header.WriteStart;
            var we = header.WriteEnd;
            var rs = header.ReadStart;
            var re = header.ReadEnd;
            return $"Nodes: {nodes} Write: [{ws}, {we}], Read: [{rs}, {re}]";
        }

        public static float PI = (float)Math.PI;
        public static float TwoPI = (float)(2.0*Math.PI);

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
        
        public static bool ArrayEquals<T>(T[] first, T[] second)
        {
            if (first == second)
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