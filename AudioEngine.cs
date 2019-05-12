using System;
using System.IO;
using VL.Lib.Animation;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public static class AudioEngine
    {
        public static Spread<float> SolveODEEuler(float dt, float t, int len, Spread<float> x,
            Func<float, Spread<float>, Spread<float>> f)
        {
            if (x.Count != len)
            {
                float[] xx = new float[len];
                x = xx.ToSpread();
            }

            var k = f(t, x);
            var r = new SpreadBuilder<float>();
            for (int i = 0; i < x.Count; i++)
            {
                r.Add(x[i] + dt * k[i]);
            }

            return r.ToSpread();
        }

        public static Spread<float> SolveODERK4(IFrameClock clock, float t, int len, Spread<float> x,
            Func<float, Spread<float>, Spread<float>> f)
        {
            var dt = (float) clock.TimeDifference;
            var k1 = f(t, x);
            var yi = new SpreadBuilder<float>();
            for (int i = 0; i < x.Count; i++)
            {
                yi.Add(x[i] + k1[i] * dt / 2.0);
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

        private static StreamWriter log = File.CreateText("out.log");

        public static void Log(string line)
        {
            log.WriteLine(line);
            log.Flush();
        }
    }
}