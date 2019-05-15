using System;
using VL.Lib.Animation;

namespace VL.NewAudio
{
    public enum ODESolverType
    {
        Euler,
        RungeKutta2,
        RungeKutta4
    }

    public class SolveODE<TState> where TState : class
    {
        private TState state;
        private int length;
        private float[] x;
        private float[] k;
        private float[] k2;
        private float[] k3;
        private float[] k4;
        private float[] yi;
        private Func<TState, float, ArrayAccessor, TState> updateFunction;
        private IFrameClock clock;
        private ArrayAccessor accessor = new ArrayAccessor();

        public SolveODE(IFrameClock clock)
        {
            this.clock = clock;
        }

        public float[] Update(Func<TState> create, float t, int len, ODESolverType type, bool reset,
            Func<TState, float, ArrayAccessor, TState> update)
        {
            if (reset || x == null || k == null || updateFunction == null)
            {
                state = create();
                x = new float[len];
                k = new float[len];
                k2 = k3 = k4 = yi = null;
                length = len;
            }

            if (update != updateFunction)
            {
                updateFunction = update;
                switch (type)
                {
                    case ODESolverType.Euler:
                        euler(t);
                        break;
                    case ODESolverType.RungeKutta2:
                        rk2(t);
                        break;
                    case ODESolverType.RungeKutta4:
                        rk4(t);
                        break;
                }
            }

            return x;
        }

        private void euler(float t)
        {
            accessor.Update(k, x);
            state = updateFunction(state, t, accessor);

            for (int i = 0; i < length; i++)
            {
                x[i] += (float) clock.TimeDifference * k[i];
            }
        }

        private void rk2(float t)
        {
            if (k2 == null || yi == null)
            {
                k2 = new float[length];
                yi = new float[length];
            }

            accessor.Update(k, x);
            state = updateFunction(state, t, accessor);
            for (int i = 0; i < length; i++)
            {
                yi[i] = x[i] + k[i] * (float) clock.TimeDifference / 2;
            }

            accessor.Update(k2, yi);
            state = updateFunction(state, t + (float) clock.TimeDifference / 2, accessor);
            for (int i = 0; i < length; i++)
            {
                x[i] += k2[i] * (float) clock.TimeDifference;
            }
        }

        private void rk4(float t)
        {
            if (k2 == null || k3 == null || k4 == null || yi == null)
            {
                k2 = new float[length];
                k3 = new float[length];
                k4 = new float[length];
                yi = new float[length];
            }

            accessor.Update(k, x);
            state = updateFunction(state, t, accessor);
            for (int i = 0; i < length; i++)
            {
                yi[i] = x[i] + k[i] * (float) clock.TimeDifference / 2;
            }

            accessor.Update(k2, yi);
            state = updateFunction(state, t + (float) clock.TimeDifference / 2, accessor);
            for (int i = 0; i < length; i++)
            {
                yi[i] = x[i] + k2[i] * (float) clock.TimeDifference / 2;
            }

            accessor.Update(k3, yi);
            state = updateFunction(state, t + (float) clock.TimeDifference / 2, accessor);
            for (int i = 0; i < length; i++)
            {
                yi[i] = x[i] + k3[i] * (float) clock.TimeDifference;
            }

            accessor.Update(k4, yi);
            state = updateFunction(state, t + (float) clock.TimeDifference, accessor);
            for (int i = 0; i < length; i++)
            {
                x[i] += (float) clock.TimeDifference * (k[i] + 2 * k2[i] + 2 * k3[i] + k4[i]) / 6;
            }
        }
    }
}