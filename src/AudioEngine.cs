using System;
using System.IO;

namespace VL.NewAudio
{
    public static class AudioEngine
    {
        public static float PI = (float) Math.PI;

        public static float TanH(float v)
        {
            return (float) Math.Tanh(v);
        }

        public static float SinF(float v)
        {
            return (float) Math.Sin(v);
        }

        public static float CosF(float v)
        {
            return (float) Math.Cos(v);
        }

        public static float SinC(float v)
        {
            if (Math.Abs(v) < 0.0000001f)
            {
                return 1.0f;
            }

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

        private static StreamWriter log = File.CreateText("out.log");

        public static void Log(string line)
        {
            log.WriteLine(line);
            log.Flush();
            log.AutoFlush = true;
        }
    }
}