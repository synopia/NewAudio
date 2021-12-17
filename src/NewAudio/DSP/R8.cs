using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace VL.NewAudio.Dsp
{
    public sealed class CDSPResampler: IDisposable
    {
        private IntPtr r8;
        private bool _disposedValue;
        public double[] InData;
        private float[] _outData;
        public RingBuffer Buffer;
        
        public CDSPResampler(double sourceSampleRate, double targetSampleRate, int maxFramesPerBlock, double transBand=2.0)
        {
            r8 = R8.r8b_create(sourceSampleRate, targetSampleRate, maxFramesPerBlock, transBand, 2);
            InData = new double[maxFramesPerBlock];
            var framesPerBlock = (int)(maxFramesPerBlock * targetSampleRate/sourceSampleRate+1);
            _outData = new float[framesPerBlock];
            Buffer = new RingBuffer(framesPerBlock * 2);
        }

        public void Reset()
        {
            R8.r8b_clear(r8);
        }

        public unsafe int Process(int numFrames)
        {
            fixed (double* input = InData)
            {
                var outLen = R8.r8b_process(r8, new IntPtr(input), numFrames, out IntPtr output);
                Trace.Assert(outLen<=_outData.Length);
                for (int i = 0; i < outLen; i++)
                {
                    _outData[i] = (float) ((double*)output.ToPointer())[i];
                }
                Trace.Assert(Buffer.AvailableWrite>=outLen);
                Buffer.Write(_outData, outLen);
                return outLen;
            }
        }
        
        public void Dispose()
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
                R8.r8b_delete(r8);
            }
        }
    }
    
    [SuppressUnmanagedCodeSecurity]
    public sealed class R8
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string file);

        [DllImport("r8bsrc", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr r8b_create(double srcSampleRate, double dstSampleRate, int maxInLen,
            double reqTransBand, int res);

        [DllImport("r8bsrc", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr r8b_delete(IntPtr rs);

        [DllImport("r8bsrc", CallingConvention = CallingConvention.Cdecl)]
        internal  static extern IntPtr r8b_clear(IntPtr rs);

        [DllImport("r8bsrc", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int r8b_process(IntPtr rs, IntPtr ip0, int len, out IntPtr output);

        static R8()
        {
            var prefix = IntPtr.Size == 4 ? "x86" : "x64";
            var location = Path.GetDirectoryName(typeof(R8).Assembly.Location);
            if (location == null)
            {
                throw new InvalidProgramException("r8bsrc.dll not found!");
            }
            var path = Path.Combine(location, prefix);
            var x = LoadLibrary(Path.Combine(path, "r8bsrc.dll"));
            Console.WriteLine(x);
        }

        internal static unsafe IntPtr R8BCreate(double sourceSampleRate, double targetSampleRate, int maxFramesPerBlock, double transBand=2.0)
        {
            return r8b_create(sourceSampleRate, targetSampleRate, maxFramesPerBlock, transBand, 2);
            
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