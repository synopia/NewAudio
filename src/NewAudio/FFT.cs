using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using FFTW.NET;
using NAudio.Dsp;
using NAudio.Utils;
using NewAudio.Internal;
using Serilog;
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
        private readonly ILogger _logger = Log.ForContext<FFT>();
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
                _logger.Information(
                    "Config Changed: format: {InputFormat}, len: {fftLength}, Window: {WindowFunction}, Direction: {fftDirection}, Output: {outputType}", input.Format, fftLength, windowFunction, fftDirection, outputType);
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
                    _logger.Error("Input must have 1 or 2 channels, actual: {channels}!", input.Format.Channels);
                    Stop();
                    return;
                }

               
                _source = new BufferBlock<AudioBuffer>();
                var action = new ActionBlock<AudioBuffer>(i =>
                {
                    try
                    {
                        _time = i.Time;
                        if (_fftDirection == FFTDirection.Forwards)
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
                        _logger.Error("{e}",e);
                    }
                });
                if (fftDirection == FFTDirection.Forwards)
                {
                    AudioFormat fftFormat = input.Format.WithSampleCount(fftLength);
                    // todo
                    var target = new AudioFlowSource(fftFormat, 4 * fftLength);
                    target.LinkTo(action);
                    _link = input.SourceBlock.LinkTo(target);
                    Output.SourceBlock = _source;
                    Output.Format = fftFormat; 
                }
                else
                {
                    AudioFormat fftFormat = input.Format.WithSampleCount(fftLength);
                    // todo
                    var target = new AudioFlowSource(fftFormat, 4 * fftLength);
                    target.LinkTo(action);
                    _link = input.SourceBlock.LinkTo(target);
                    Output.SourceBlock = _source;
                    Output.Format = new AudioFormat(OutputChannels, input.Format.SampleRate, 256);
                }
            }
        }

        private void CopyInputReal(AudioBuffer input)
        {
            if (_inputBufferReal == null || _inputBufferReal.Length != _fftLength)
            {
                _inputBufferReal = new double[_fftLength];
            }

            _logger.Verbose("input: {inputLen}, data: {dataLen}, window: {windowLen}", _inputBufferReal.Length, input.Data.Length, _window.Length);
            for (int i = 0; i < _fftLength; i++)
            {
                _inputBufferReal[i] = input.Data[i] * _window[i];
            }
        }

        private void CopyInputComplex(AudioBuffer input)
        {
            if (_inputBufferComplex == null || _inputBufferComplex.Length != (_fftLength-1)/2+1)
            {
                _inputBufferComplex = new Complex[(_fftLength-1)/2+1];
            }
            _logger.Verbose("{inputLen} {_fftLength} {windowLength} {inputBufferComplexLength}", input.Data.Length, _fftLength, _window.Length, _inputBufferComplex.Length);

            for (int i = 0; i < (_fftLength-1)/2+1; i++)
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
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer( _fftLength/2);
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
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer(_fftLength);
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
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer( _fftLength/2);
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
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer( _fftLength);
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
            for (int b = 0; b < _fftLength / 256; b++)
            {
                outputBuffer = AudioCore.Instance.BufferFactory.GetBuffer(256);
                for (int i = 0; i < 256; i++)
                {
                    outputBuffer.Data[i % 256] = (float)output[b*256+i] / _fftLength;
                }

                outputBuffer.Data[0] = 0;
                _source.Post(outputBuffer);
            }
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
            double[] o = new double[input.Data.Length];
            // CopyInputComplex(input);
            using var pinIn = new PinnedArray<Complex>((input.Data.Length-1)/2+2);
            using var output = new PinnedArray<double>(o);
            // CopyInputComplex(input);
            /*
            using var output = new PinnedArray<double>(o);
            using var pinIn = new FftwArrayComplex(DFT.GetComplexBufferSize(output.GetSize()));
            */
            for (int i = 0; i < pinIn.Length-1; i++)
            {
                pinIn[i+1] = new Complex(input.Data[i*2], input.Data[i*2+1]);
            }

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