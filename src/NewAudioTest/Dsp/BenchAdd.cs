using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using NUnit.Framework;

namespace VL.NewAudioTest.Dsp
{
    
    [TestFixture]
    [DisassemblyDiagnoser()]
    public class BenchAdd
    {
        private const int Len = 512;
        private float[] _buf1 = new float[Len];
        private float[] _buf2 = new float[Len];
        
        [Benchmark(Baseline = true)]
        public void Baseline()
        {
            for (int i = 0; i < Len; i++)
            {
                _buf1[i] += _buf2[i];
            }
        }

        [Benchmark]
        public void Vectors()
        {
            var l = Vector<float>.Count;
            int ceiling = Len / l * l;
            for (int i = 0; i < Len; i+=l)
            {
                var b1 = new Vector<float>(_buf1, i);
                var b2 = new Vector<float>(_buf2, i);
                Vector.Add(b1,b2).CopyTo(_buf1,i);
            }

            for (int i = ceiling; i < Len; i++)
            {
                _buf1[i] += _buf2[i];
            }
        }
        
        [Benchmark]
        public unsafe void VectorsUnsafe()
        {
            var l = Vector<float>.Count;
            int ceiling = Len / l * l;

            fixed (float* buf1 = _buf1, buf2 = _buf2)
            {
                for (int i = 0; i < ceiling; i += l)
                {
                    Unsafe.Write(buf1+i, Unsafe.Read<Vector<float>>(buf1+i)+Unsafe.Read<Vector<float>>(buf2+i));
                }
                
            }
            
            for (int i = ceiling; i < Len; i++)
            {
                _buf1[i] += _buf2[i];
            }
        }
        
        [Benchmark]
        public void VectorsNoCopy()
        {
            var l = Vector<float>.Count;
            int numVectors = Len / l;
            int ceiling = numVectors * l;

            ReadOnlySpan<Vector<float>> b1Arr = MemoryMarshal.Cast<float, Vector<float>>(_buf1.AsSpan());
            ReadOnlySpan<Vector<float>> b2Arr = MemoryMarshal.Cast<float, Vector<float>>(_buf2.AsSpan());
            Span<Vector<float>> res = MemoryMarshal.Cast<float, Vector<float>>(_buf1.AsSpan());
            for (int i = 0; i < numVectors; i++)
            {
                res[i] = b1Arr[i] + b2Arr[i];
            }
        }

        
        [Test]
        public void Test()
        {
            BenchmarkRunner.Run<BenchAdd>(ManualConfig.Create(DefaultConfig.Instance).WithOption(ConfigOptions.DisableOptimizationsValidator,true));
        }
    }
}