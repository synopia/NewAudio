﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace VL.NewAudio.Dsp
{
    [SuppressUnmanagedCodeSecurity]
    public sealed class R8
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string file);

        [DllImport("r8bsrc", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr r8b_create(double srcSampleRate, double dstSampleRate, int maxInLen,
            double reqTransBand, int res);

        [DllImport("r8bsrc", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr r8b_delete(IntPtr rs);

        [DllImport("r8bsrc", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr r8b_clear(IntPtr rs);

        [DllImport("r8bsrc", CallingConvention = CallingConvention.Cdecl)]
        private static extern int r8b_process(IntPtr rs, IntPtr ip0, int len, out IntPtr output);

        static R8()
        {
            var prefix = IntPtr.Size == 4 ? "x86" : "x64";
            var location = Path.GetDirectoryName(typeof(R8).Assembly.Location);
            var path = Path.Combine(location, prefix);
            LoadLibrary(Path.Combine(path, "r8bsrc.dll"));
        }

        public static unsafe void R8bCreate()
        {
            var d = new double[1000];
            var o = new double[1000];
            for (var i = 0; i < 1000; i++)
            {
                d[i] = i / 1000.0f;
            }

            fixed (void* ip0 = d)
            fixed (void* output = o)
            {
                var x = r8b_create(44100, 50000, 1000, 1, 2);
                while (true)
                {
                    var r = r8b_process(x, new IntPtr(ip0), 1000, out var op);
                    for (var i = 0; i < r; i++)
                    {
                        o[i] = ((double*)op.ToPointer())[i];
                    }

                    Trace.WriteLine(o[50]);
                }

                r8b_delete(x);
            }

            Trace.WriteLine(d[50]);
            Trace.WriteLine(o[50]);
        }
    }
}