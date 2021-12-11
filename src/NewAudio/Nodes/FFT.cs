using System.Diagnostics;
using System.Numerics;
using FFTW.NET;
using static VL.NewAudio.Dsp.AudioMath;
using Serilog;

namespace VL.NewAudio.Nodes
{
    public abstract class BaseFft
    {
        private readonly ILogger _logger = Resources.GetLogger<BaseFft>();

        protected double[]? Window;
        protected int FftLength;
        protected float[] Data;
        private WindowFunction _windowFunction = WindowFunction.None;

        private bool _disposedValue;

        protected BaseFft()
        {
            _logger.Information("FFT created");
        }

        public void DoFft(float[] data, int fftLength, WindowFunction windowFunction)
        {
            Data = data;
            FftLength = fftLength;
            if (FftLength != Window?.Length)
            {
                _windowFunction = windowFunction;
                Window = CreateWindow(_windowFunction, FftLength);
                ResizeBuffers(FftLength, (FftLength - 1) / 2 + 2);
            }
            else if (windowFunction != _windowFunction)
            {
                _windowFunction = windowFunction;
                Window = CreateWindow(windowFunction, FftLength);
            }

            OnDataReceived();
        }

        protected abstract void OnDataReceived();
        protected abstract void ResizeBuffers(int fftLength, int complexLength);

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.Information("Disposing audio node {@This}", this);
                }

                _disposedValue = true;
            }
        }
    }

    public class ForwardFft : BaseFft
    {
        private PinnedArray<double>? _pinIn;
        private PinnedArray<Complex>? _pinOut;

        protected override void ResizeBuffers(int fftLength, int complexLength)
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();

            _pinIn = new PinnedArray<double>(fftLength);
            _pinOut = new PinnedArray<Complex>(complexLength);
        }

        private void CopyInputReal()
        {
            Trace.Assert(_pinIn != null && _pinOut != null);

            var j = 0;
            for (var i = 0; i < FftLength; i++)
            {
                _pinIn![i] = Data[j++] * Window[i];
            }
        }

        private void CopyOutputComplex()
        {
            Trace.Assert(_pinIn != null && _pinOut != null);
            var written = 0;
            for (var i = 0; i < FftLength / 2; i++)
            {
                Data[written++] = (float)_pinOut![i].Real;
                Data[written++] = (float)_pinOut[i].Imaginary;
            }
        }

        protected override void OnDataReceived()
        {
            CopyInputReal();
            DFT.FFT(_pinIn, _pinOut);
            CopyOutputComplex();
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _pinIn?.Dispose();
                    _pinOut?.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }

    public class BackwardFft : BaseFft
    {
        private PinnedArray<Complex>? _pinIn;
        private PinnedArray<double>? _pinOut;

        protected override void ResizeBuffers(int fftLength, int complexLength)
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();

            _pinIn = new PinnedArray<Complex>(complexLength);
            _pinOut = new PinnedArray<double>(fftLength);
        }

        private void CopyInputComplex()
        {
            Trace.Assert(_pinIn != null && _pinOut != null);
            var j = 0;
            for (var i = 0; i < _pinIn!.Length - 1; i++)
            {
                _pinIn[i + 1] = new Complex(Data[j], Data[j + 1]);
                j += 2;
            }
        }

        private void CopyOutputReal()
        {
            Trace.Assert(_pinIn != null && _pinOut != null);
            var written = 0;
            for (var b = 0; b < FftLength; b++)
            {
                Data[written++] = (float)_pinOut![b] / FftLength;
            }
        }

        protected override void OnDataReceived()
        {
            CopyInputComplex();
            DFT.IFFT(_pinIn, _pinOut);
            CopyOutputReal();
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _pinIn?.Dispose();
                    _pinOut?.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}