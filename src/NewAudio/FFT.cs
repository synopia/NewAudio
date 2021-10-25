using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using FFTW.NET;
using NAudio.Dsp;
using NAudio.Utils;
using NewAudio.Internal;
using VL.Lib.Collections;
using Complex = System.Numerics.Complex;

namespace NewAudio
{
    public enum FFTDirection
    {
        Forwards,
        Backwards
    }

    public enum FFTType
    {
        Real = 1,
        Complex = 2
    }

    public enum WindowFunction
    {
        None,
        Hamming,
        Hann,
        BlackmanHarris
    }

    public class FFT : AudioNodeTransformer
    {
        private readonly Logger _logger = LogFactory.Instance.Create("FFT");
        private int _fftLength;
        private WindowFunction _windowFunction;
        private FFTDirection _fftDirection;
        private FFTType _outputType;
        private FFTType _inputType;

        private IDisposable _link;

        private double[] _window;
        private double[] _inputBufferReal;
        private Complex[] _inputBufferComplex;

        private BufferBlock<AudioBuffer> _source;

        public int OutputChannels => (int)_outputType;

        public WindowFunction WindowFunction
        {
            get => _windowFunction;
            set
            {
                if (_window == null || _windowFunction != value || _window.Length!=_fftLength)
                {
                    _windowFunction = value;
                    _window = FFTUtils.CreateWindow(value, _fftLength);
                }
            }
        }

        private int _time;

        public void ChangeSettings(AudioLink input, int fftLength, WindowFunction windowFunction,
            FFTDirection fftDirection = FFTDirection.Forwards, FFTType outputType = FFTType.Real)
        {
            Stop();

            _fftLength = (int)FFTUtils.UpperPow2((uint)fftLength);
            WindowFunction = windowFunction;
            _fftDirection = fftDirection;
            _outputType = outputType;

            Connect(input);

            if (_link == null && input != null && _fftLength > 0)
            {
                _logger.Info(
                    $"Config Changed: format: {input.Format}, len: {fftLength}, Window: {WindowFunction}, Direction: {fftDirection}, Output: {outputType}");
                if (input.Format.Channels == 1)
                {
                    _inputType = FFTType.Real;
                }
                else if (input.Format.Channels == 2)
                {
                    _inputType = FFTType.Complex;
                }
                else
                {
                    _logger.Error($"Input must have 1 or 2 channels, actual: {input.Format.Channels}!");
                    Stop();
                    return;
                }

                int fftBufferSize = fftLength;
                int bufferSize = fftBufferSize;
                if (input.Format.BufferSize > bufferSize)
                {
                    bufferSize = 512;
                }

                var target = new AudioFlowBuffer(input.Format, bufferSize, fftLength);
                _source = new BufferBlock<AudioBuffer>();
                var action = new ActionBlock<AudioBuffer>(i =>
                {
                    try
                    {
                        _time = i.Time;
                        if (fftDirection == FFTDirection.Forwards)
                        {
                            if (_inputType == FFTType.Real)
                            {
                                DoFFT_R2C(i);
                            }
                            else
                            {
                                DoFFT_C2C(i);
                            }
                        }
                        else
                        {
                            if (_outputType == FFTType.Real)
                            {
                                DoIFFT_C2R(i);
                            }
                            else
                            {
                                DoIFFT_C2C(i);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e);
                    }
                });
                target.LinkTo(action);
                _link = input.SourceBlock.LinkTo(target);
                Output.SourceBlock = _source;
                Output.Format = new AudioFormat(OutputChannels, input.Format.SampleRate, fftLength/2);
            }
        }

        private void CopyInputReal(AudioBuffer input)
        {
            if (_inputBufferReal == null || _inputBufferReal.Length != _fftLength)
            {
                _inputBufferReal = new double[_fftLength];
            }

            for (int i = 0; i < _fftLength; i++)
            {
                _inputBufferReal[i] = input.Data[i] * _window[i];
            }
        }

        private void CopyInputComplex(AudioBuffer input)
        {
            if (_inputBufferComplex == null || _inputBufferComplex.Length != _fftLength/2)
            {
                _inputBufferComplex = new Complex[_fftLength/2];
            }

            for (int i = 0; i < _fftLength/2; i++)
            {
                if (_inputType==FFTType.Real)
                {
                    _inputBufferComplex[i] = new Complex(input.Data[i] * _window[i], 0);
                }
                else
                {
                    _inputBufferComplex[i] = new Complex(input.Data[i * 2] * _window[i], input.Data[i * 2 + 1]);
                }
            }
        }

        private void CopyOutput(FftwArrayComplex output)
        {
            AudioBuffer outputBuffer;
            if (_outputType == FFTType.Real)
            {
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer(_time, _fftLength/2);
                for (int i = 1; i < _fftLength/2; i++)
                {
                    var real = (float)output[i].Real;
                    var img = (float)output[i].Imaginary;
                    outputBuffer.Data[i] = (float)Math.Sqrt(real * real + img * img);
                }

                outputBuffer.Data[0] = 0;
            }
            else
            {
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer(_time, _fftLength);
                for (int i = 0; i < _fftLength/2; i++)
                {
                    outputBuffer.Data[i * 2] = (float)output[i].Real;
                    outputBuffer.Data[i * 2 + 1] = (float)output[i].Imaginary;
                }
            }

            _source.Post(outputBuffer);
        }
        private void CopyOutput2(PinnedArray<Complex> output)
        {
            AudioBuffer outputBuffer;
            if (_outputType == FFTType.Real)
            {
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer(_time, _fftLength/2);
                for (int i = 1; i < _fftLength/2; i++)
                {
                    var real = (float)output[i].Real;
                    var img = (float)output[i].Imaginary;
                    outputBuffer.Data[i] = (float)Math.Sqrt(real * real + img * img);
                }

                outputBuffer.Data[0] = 0;
            }
            else
            {
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer(_time, _fftLength);
                for (int i = 0; i < _fftLength/2; i++)
                {
                    outputBuffer.Data[i * 2] = (float)output[i].Real;
                    outputBuffer.Data[i * 2 + 1] = (float)output[i].Imaginary;
                }
            }

            _source.Post(outputBuffer);
        }
        private void CopyOutput3(double[] output)
        {
            AudioBuffer outputBuffer;
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer(_time, _fftLength/2);
                for (int i = 1; i < _fftLength/2; i++)
                {
                    outputBuffer.Data[i] = (float)output[i];
                }

                outputBuffer.Data[0] = 0;
            _source.Post(outputBuffer);
        }

        private void DoFFT_R2C(AudioBuffer input)
        {
            CopyInputReal(input);
            using var pinIn = new PinnedArray<double>(_inputBufferReal);
            using var output = new FftwArrayComplex(DFT.GetComplexBufferSize(pinIn.GetSize()));
            DFT.FFT(pinIn, output);
            CopyOutput(output);
        }

        private void DoFFT_C2C(AudioBuffer input)
        {
            CopyInputComplex(input);
            using var pinIn = new PinnedArray<Complex>(_inputBufferComplex);
            using var output = new FftwArrayComplex(DFT.GetComplexBufferSize(pinIn.GetSize()));
            DFT.FFT(pinIn, output);
            CopyOutput(output);
        }

        private void DoIFFT_C2R(AudioBuffer input)
        {
            double[] o = new double[_fftLength];
            CopyInputComplex(input);
            using var pinIn = new PinnedArray<Complex>(_inputBufferComplex);
            using var output = new PinnedArray<double>(o);
            DFT.IFFT(pinIn, output);
            CopyOutput3(o);
        }

        private void DoIFFT_C2C(AudioBuffer input)
        {
            Complex[] o = new Complex[_fftLength/2];
            CopyInputComplex(input);
            using var pinIn = new PinnedArray<Complex>(_inputBufferComplex);
            using var output = new PinnedArray<Complex>(o);
            DFT.IFFT(pinIn, output);
            CopyOutput2(output);
        }

        public void Stop()
        {
            _link?.Dispose();
            _link = null;
        }

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }

    }
}