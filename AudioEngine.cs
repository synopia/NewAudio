using System;
using System.IO;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public static class AudioEngine
    {
        public static float PI = (float) Math.PI;

        public static float GetSample(float[] buffer, int index, int channel = 0)
        {
            return buffer?[channel + index] ?? 0;
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

        public static Spread<float> SolveODEEuler(float dt, float t, int len, Spread<float> x,
            Func<float, Spread<float>, Spread<float>> f)
        {
            try
            {
                if (x.Count != len)
                {
                    float[] xx = new float[len];
                    x = xx.ToSpread();
                }

                var k = f(t, x);
                if (k.Count != len)
                {
                    return x;
                }

                var r = new SpreadBuilder<float>();
                for (int i = 0; i < x.Count; i++)
                {
                    r.Add(x[i] + dt * k[i]);
                }

                return r.ToSpread();
            }
            catch (Exception e)
            {
                Log(e.Message);
                return x;
            }
        }

        public static Spread<float> SolveODERK4(float dt, float t, int len, Spread<float> x,
            Func<float, Spread<float>, Spread<float>> f)
        {
            try
            {
                if (x.Count != len)
                {
                    float[] xx = new float[len];
                    x = xx.ToSpread();
                }

                var k1 = f(t, x);
                var yi = new SpreadBuilder<float>();
                for (int i = 0; i < x.Count; i++)
                {
                    yi.Add((float) (x[i] + k1[i] * dt / 2.0));
                }

                var k2 = f(t + dt / 2, yi.ToSpread());
                for (int i = 0; i < x.Count; i++)
                {
                    yi[i] = x[i] + k2[i] * dt / 2;
                }

                var k3 = f(t + dt / 2, yi.ToSpread());
                for (int i = 0; i < x.Count; i++)
                {
                    yi[i] = x[i] + k3[i] * dt / 2;
                }

                var k4 = f(t + dt / 2, yi.ToSpread());
                var r = new SpreadBuilder<float>();
                for (int i = 0; i < x.Count; i++)
                {
                    r.Add(x[i] + dt * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]) / 6);
                }

                return r.ToSpread();
            }
            catch (Exception e)
            {
                Log($"{e.Message}");
                return x;
            }
        }

        private static StreamWriter log = File.CreateText("out.log");

        public static void Log(string line)
        {
            log.WriteLine(line);
            log.Flush();
        }
    }
}