using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using NAudio.Wave.Asio;
using NewAudio.Dsp;
using NUnit.Framework;

namespace NewAudioTest
{
    public class Baseline
    {
        private unsafe void SetOutputSampleInt32LSB(IntPtr buffer, int n, float value)
        {
            *((int*)buffer + n) = (int)(value * int.MaxValue);
        }

        private unsafe float GetInputSampleInt32LSB(IntPtr inputBuffer, int n)
        {
            return *((int*)inputBuffer + n) / (float)int.MaxValue;
        }

        private unsafe float GetInputSampleInt16LSB(IntPtr inputBuffer, int n)
        {
            return *((short*)inputBuffer + n) / (float)short.MaxValue;
        }

        private unsafe void SetOutputSampleInt16LSB(IntPtr buffer, int n, float value)
        {
            *((short*)buffer + n) = (short)(value * short.MaxValue);
        }

        private unsafe float GetInputSampleInt24LSB(IntPtr inputBuffer, int n)
        {
            byte* pSample = (byte*)inputBuffer + n * 3;
            int sample = pSample[0] | (pSample[1] << 8) | ((sbyte)pSample[2] << 16);
            return sample / 8388608.0f;
        }


        private unsafe float GetInputSampleFloat32LSB(IntPtr inputBuffer, int n)
        {
            return *((float*) inputBuffer + n);
        }

        private unsafe void SetOutputSampleFloat32LSB(IntPtr buffer, int n, float value)
        {
            *((float*) buffer + n) = value;
        }
        public unsafe void ConvertFrom(IntPtr[] source, float[] target,  int numFrames, int numChannels, AsioSampleType sampleType)
        {
            Func<IntPtr, int, float> getInputSample;
            if (sampleType == AsioSampleType.Int32LSB)
                getInputSample = GetInputSampleInt32LSB;
            else if (sampleType == AsioSampleType.Int16LSB)
                getInputSample = GetInputSampleInt16LSB;
            else if (sampleType == AsioSampleType.Int24LSB)
                getInputSample = GetInputSampleInt24LSB;
            else if (sampleType == AsioSampleType.Float32LSB)
                getInputSample = GetInputSampleFloat32LSB;
            else throw new Exception();
            
            int offset = 0;
            for (int n = 0; n < numFrames; n++)
            {
                for (int inputChannel = 0; inputChannel < numChannels; inputChannel++)
                {
                    target[offset] = getInputSample(source[inputChannel], n);
                }
            }
        }
        public unsafe void ConvertTo(float[] source, IntPtr[] target, int numFrames, int numChannels, AsioSampleType sampleType)
        {
            Action<IntPtr, int, float> setOutputSample;
            if (sampleType == AsioSampleType.Int32LSB)
                setOutputSample = SetOutputSampleInt32LSB;
            else if (sampleType == AsioSampleType.Int16LSB)
                setOutputSample = SetOutputSampleInt16LSB;
            else if (sampleType == AsioSampleType.Int24LSB)
                throw new InvalidOperationException("Not supported");
            else if (sampleType == AsioSampleType.Float32LSB)
                setOutputSample = SetOutputSampleFloat32LSB;
            else throw new Exception();
            
            int offset = 0;
            for (int n = 0; n < numFrames; n++)
            {
                for (int outputChannel = 0; outputChannel < numChannels; outputChannel++)
                {
                    setOutputSample(target[outputChannel], n, source[offset++]);
                }
            }
        }
    }
    [TestFixture]
    [DisassemblyDiagnoser()]
    public class BenchTest
    {
        private Converter<Float32Sample> _f32 = new ();
        private Converter<Int16LsbSample> _c16 = new ();
        private Converter<Int24LsbSample> _c24 = new ();
        private Converter<Int32LsbSample> _c32 = new ();
        private Baseline _baseline = new Baseline();

        private float[] _source;
        private IntPtr[] _target;
        private float[][] _buffers;
        private GCHandle[] _handles;
        private int _frames;
        private int _channels;

        public BenchTest()
        {
            _frames = 1024;
            _channels = 2;
            _source = new float[_frames * _channels];
            _target = new IntPtr[_channels];
            _handles = new GCHandle[_channels];
            _buffers = new float[_channels][];
            for (int c = 0; c < _channels; c++)
            {
                _buffers[c] = new float[_frames];
                _handles[c] = GCHandle.Alloc(_buffers[c], GCHandleType.Pinned);
                _target[c] = _handles[c].AddrOfPinnedObject();
            }

        }

        [Benchmark(Baseline = true)]
        public void SFloat() =>
            _baseline.ConvertTo(_source, _target, _frames, _channels, AsioSampleType.Float32LSB);
        [Benchmark]
        public void SInt16() =>
            _baseline.ConvertTo(_source, _target, _frames, _channels, AsioSampleType.Int16LSB);
        [Benchmark]
        public void SInt24() =>
            _baseline.ConvertTo(_source, _target, _frames, _channels, AsioSampleType.Int24LSB);
        [Benchmark]
        public void SInt32() =>
            _baseline.ConvertTo(_source, _target, _frames, _channels, AsioSampleType.Int32LSB);

        [Benchmark]
        public void FFloat() =>
            _f32.ConvertTo(_source, _target, _frames, 0, _channels);
        [Benchmark]
        public void FInt16() =>
            _c16.ConvertTo(_source, _target, _frames,0,  _channels);
        [Benchmark]
        public void FInt24() =>
            _c24.ConvertTo(_source, _target, _frames, 0, _channels);
        [Benchmark]
        public void FInt32() =>
            _c32.ConvertTo(_source, _target, _frames, 0, _channels);
        
        [Test]
        public void Test()
        {
            BenchmarkRunner.Run<BenchTest>(ManualConfig.Create(DefaultConfig.Instance).WithOption(ConfigOptions.DisableOptimizationsValidator,true));
        }
    }
}