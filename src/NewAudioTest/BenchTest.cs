using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using FFTW.NET;
using NAudio.Wave.Asio;
using NewAudio.Dsp;
using NUnit.Framework;
using NewAudio.Internal;
using Xt;

namespace NewAudioTest
{
    public interface BenchCase : IDisposable
    {
        public void Read(XtBuffer buffer, AudioBuffer target);
        public void Write(XtBuffer buffer, AudioBuffer target);
    }
    [DisassemblyDiagnoser(maxDepth:5,printSource:true)]
    public class Baseline : BenchCase
    {
        private SafeBuffer _safeBuffer;
        private bool _interleaved;
        private XtFormat _format;

        public Baseline(bool interleaved, XtFormat format, int frames)
        {
            _format = format;
            _interleaved = interleaved;
            _safeBuffer = new SafeBuffer(interleaved, format, frames);
        }
        public void Read(XtBuffer buffer, AudioBuffer target)
        {
            _safeBuffer.Lock(buffer);
            if (_interleaved)
            {
                float[] output = (float[])_safeBuffer.GetInput();
                for (int i = 0; i < buffer.frames; i++)
                {
                    target[i] = output[i];
                }
            }
            else
            {
                float[][] output = (float[][])_safeBuffer.GetInput();
                for (int ch = 0; ch < _format.channels.inputs; ch++)
                {
                    for (int i = 0; i < buffer.frames; i++)
                    {
                        target[i+ch*buffer.frames] = output[ch][i];
                    }
                }
            }

            _safeBuffer.Unlock(buffer);
        }
        public void Write(XtBuffer buffer, AudioBuffer target)
        {
            _safeBuffer.Lock(buffer);
            if (_interleaved)
            {
                float[] output = (float[])_safeBuffer.GetOutput();
                for (int i = 0; i < buffer.frames; i++)
                {
                    output[i] = target[i];
                }
            }
            else
            {
                float[][] output = (float[][])_safeBuffer.GetOutput();
                for (int ch = 0; ch < _format.channels.outputs; ch++)
                {
                    for (int i = 0; i < buffer.frames; i++)
                    {
                        output[ch][i] = target[i+ch*buffer.frames];
                    }
                }
            }

            _safeBuffer.Unlock(buffer);
        }

        public void Dispose()
        {
            _safeBuffer.Dispose();
        }
    }
    [DisassemblyDiagnoser(maxDepth:5,printSource:true)]

    public class Convert : BenchCase
    {
        private IConvertWriter _writer;
        private IConvertReader _reader;
        private int _inputs;
        private int _outputs;
        private bool _interleaved;
        public Convert(bool interleaved, XtFormat format, int frames)
        {
            _interleaved = interleaved;
            _inputs = format.channels.inputs;
            _outputs = format.channels.outputs;
            if (interleaved)
            {
                _writer = new ConvertWriter<Float32Sample, Interleaved>();
                _reader = new ConvertReader<Float32Sample, Interleaved>();
            }
            else
            {
                _writer = new ConvertWriter<Float32Sample, NonInterleaved>();
                _reader = new ConvertReader<Float32Sample, NonInterleaved>();
            }
        }
        public void Read(XtBuffer buffer, AudioBuffer target)
        {
            if (_interleaved)
            {
                new ConvertReader<Float32Sample, Interleaved>().Read(buffer, 0, target, 0, buffer.frames);
            }
            else
            {
                new ConvertReader<Float32Sample, NonInterleaved>().Read(buffer, 0, target, 0, buffer.frames);
            }
            // _reader.Read(buffer.input, 0, target, 0, buffer.frames, _inputs);
        }

        public void Write(XtBuffer buffer, AudioBuffer source)
        {
            if (_interleaved)
            {
                new ConvertWriter<Float32Sample, Interleaved>().Write(source, 0, buffer, 0, buffer.frames);
            }
            else
            {
                new ConvertWriter<Float32Sample, NonInterleaved>().Write(source, 0, buffer, 0, buffer.frames);
            }
            // _writer.Write(source, 0, buffer.output, 0, buffer.frames, _outputs);
        }

        public void Dispose()
        {
        }
    }
    [TestFixture]
    [DisassemblyDiagnoser(maxDepth:5,printSource:true)]
    public class BenchTest : IDisposable
    {
        private Baseline _baseline;
        private Convert _converter;
        private XtBuffer _buffer;
        private AudioBuffer _target;
        private PinnedArray<float> _inputPin;
        private PinnedArray<float> _outputPin;
        private PointerPointer _inputPp;
        private PointerPointer _outputPp;

        public BenchTest()
        {
            bool interleaved = false;
            var format = new XtFormat(new XtMix(48000, XtSample.Float32), new XtChannels(2, 0, 2, 0));
            int frames = 1024;
            _baseline = new Baseline(interleaved, format, frames);
            _converter = new Convert(interleaved, format, frames);

            _target = new AudioBuffer(frames, 2);
            var input = new float[frames * format.channels.inputs];
            var output = new float[frames * format.channels.inputs];
            _inputPin = new PinnedArray<float>(input);
            _outputPin = new PinnedArray<float>(output);
            _inputPp = new PointerPointer(typeof(float), format.channels.inputs, frames);
            _outputPp = new PointerPointer(typeof(float), format.channels.outputs, frames);
            _buffer = new XtBuffer()
            {
                frames = frames,
                // input = _inputPin.Pointer,
                // output = _outputPin.Pointer
                input =  _inputPp.Pointer,
                output = _outputPp.Pointer
            };
        }

        public void Dispose()
        {
            _inputPin.Dispose();
            _outputPin.Dispose();
            _inputPp.Dispose();
            _outputPp.Dispose();
            _baseline.Dispose();
            _converter.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void Baseline()
        {
            _baseline.Read(_buffer, _target);
            _baseline.Write(_buffer, _target);
        }

        [Benchmark]
        public void Convert()
        {
            _converter.Read(_buffer, _target);
            _converter.Write(_buffer, _target);
        }

       
        [Test]
        public void TestBaseline()
        {
            Baseline();
        }
        [Test]
        public void TestConvert()
        {
            Convert();
        }
        
        // [Test]
        public void Test()
        {
            BenchmarkRunner.Run<BenchTest>(ManualConfig.Create(DefaultConfig.Instance).
                WithOption(ConfigOptions.DisableOptimizationsValidator,true));
        }
    }
}