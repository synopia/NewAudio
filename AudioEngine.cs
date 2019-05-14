using System;
using System.IO;

namespace VL.NewAudio
{
    public static class AudioEngine
    {
        public static float PI = (float) Math.PI;

        public static float[] cons(params float[] items)
        {
            return items;
        }

        public static float GetSample(float[] buffer, int index, int channel = 0)
        {
            return buffer?[index + channel] ?? 0;
        }

        public static void SetSample(float[] buffer, int index, float sample, int channel = 0)
        {
            buffer[index + channel] = sample;
        }

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

        private static StreamWriter log = File.CreateText("out.log");

        public static void Log(string line)
        {
            log.WriteLine(line);
            log.Flush();
            log.AutoFlush = true;
        }
    }
}