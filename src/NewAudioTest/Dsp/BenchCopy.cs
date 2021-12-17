using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using NUnit.Framework;
using VL.NewAudio.Dsp;

namespace VL.NewAudioTest.Dsp
{
    [TestFixture]
    [DisassemblyDiagnoser()]

    public class BenchCopy
    {
        private const int CopyStart = 12;
        private const int CopyLen = 400;
        private const int Len = 512;
        private float[] _sourceArr = new float[Len];
        private float[] _targetArr = new float[Len];
        private Memory<float> _sourceMem = new(new float[Len]);
        private Memory<float> _targetMem = new(new float[Len]);
        private Memory<float> _sourceMem2;
        private Memory<float> _targetMem2;

        public BenchCopy()
        {
            _sourceMem2 = _sourceMem.Slice(CopyStart, CopyLen);
            _targetMem2 = _targetMem.Slice(CopyStart, CopyLen);
        }

        [Benchmark(Baseline = true)]
        public void Baseline()
        {
            for (int i = CopyStart; i < CopyLen+CopyStart; i++)
            {
                _targetArr[i] = _sourceArr[i];
            }
        }

        [Benchmark]
        public void CopyArraySpan1()
        {
            _sourceArr.AsSpan().Slice(CopyStart, CopyLen).CopyTo(_targetArr.AsSpan());
        }
        [Benchmark]
        public void CopyArraySpan2()
        {
            _sourceArr.AsSpan().Slice(CopyStart, CopyLen).CopyTo(_targetArr.AsSpan().Slice(CopyStart, CopyLen));
        }

        [Benchmark]
        public void CopyMem11()
        {
            _sourceMem.Slice(CopyStart, CopyLen).CopyTo(_targetMem.Slice(CopyStart, CopyLen));
        }
        [Benchmark]
        public void CopyMem12()
        {
            
            _sourceMem.Span.Slice(CopyStart, CopyLen).CopyTo(_targetMem.Span.Slice(CopyStart, CopyLen));
        }
        [Benchmark]
        public void CopyMem21()
        {
            _sourceMem2.CopyTo(_targetMem2);
        }
        [Benchmark]
        public void CopyMem22()
        {
            
            _sourceMem2.Span.CopyTo(_targetMem2.Span);
        }
        [Benchmark]
        public unsafe void CopyVectorsUnsafe()
        {
            var l = Vector<float>.Count;
            int ceiling = CopyLen / l * l + CopyStart;

            fixed (float* buf1 = _targetArr, buf2 = _sourceArr)
            {
                for (int i = CopyStart; i < ceiling; i += l)
                {
                    Unsafe.Write(buf1+i, Unsafe.Read<Vector<float>>(buf2+i));
                }
                
            }
            
            for (int i = ceiling; i < Len; i++)
            {
                _targetArr[i] = _sourceArr[i];
            }
        }
        
        public void Test()
        {
            BenchmarkRunner.Run<BenchCopy>(ManualConfig.Create(DefaultConfig.Instance).WithOption(ConfigOptions.DisableOptimizationsValidator,true));
        }
    }
}