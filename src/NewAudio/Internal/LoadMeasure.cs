using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace VL.NewAudio.Internal
{
    public record ScopedMeasure: IDisposable
    {
        private Stopwatch _stopwatch = new ();
        private string _name;

        public ScopedMeasure(string name)
        {
            _name = name;
            _stopwatch.Start();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            LoadMeasure.Stack.Push((_name,_stopwatch.Elapsed.TotalMilliseconds));
        }
    }
    public static class LoadMeasure
    {
        internal static Stack<(string,double)> Stack = new ();
        private static ScopedMeasure _outer;
        private static double _msAvailable;
        private static double _msCpuUsage;
        public static double CpuUsage { get; set; }
        public static int XRuns { get; private set; }

        public static void Start(double msAvailable)
        {
            Stack.Clear();
            _outer = new ScopedMeasure("Outer");
            _msAvailable = msAvailable;
        }

        public static void Stop()
        {
            _outer.Dispose();
            var (_,msNeeded) = Stack.Peek();
            if (msNeeded > _msAvailable)
            {
                XRuns++;
            }
            _msCpuUsage += (msNeeded - _msCpuUsage) * 0.2;
            CpuUsage = _msCpuUsage / _msAvailable;
        }
    }
}