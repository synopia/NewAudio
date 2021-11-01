using System;
using System.Numerics;
using System.Threading.Tasks.Dataflow;
using FFTW.NET;
using NewAudio.Internal;
using Serilog;

namespace NewAudio
{


    public abstract class BaseFFT : AudioNodeTransformer
    {
        private readonly ILogger _logger = Log.ForContext<BaseFFT>();
        protected int FftLength;
        private WindowFunction _windowFunction;
        private IDisposable _link;
        protected double[] Window;

        protected BufferBlock<AudioBuffer> Source;

        private AudioTime _time;

        public void ChangeSettings(AudioLink input, int fftLength, WindowFunction windowFunction)
        {
         
            _logger.Information("Old format: {InputFormat}, len: {fftLength}, Window: {WindowFunction}",
                Input?.Format, FftLength, _windowFunction);
            Stop();

            _windowFunction = windowFunction;
            var oldLen = FftLength;
            FftLength = (int)FFTUtils.UpperPow2((uint)fftLength);
            if (oldLen != FftLength)
            {
                ResizeBuffers(FftLength, (FftLength - 1) / 2 + 2);
                Window = FFTUtils.CreateWindow(_windowFunction, FftLength);
            }

            Connect(input);

            if (_link == null && input != null && FftLength > 0)
            {
                try
                {
                    if (!ValidateInput(input))
                    {
                        _logger.Warning("{inputFormat} is not accepted!", input.Format);
                        Stop();
                        return;
                    }

                    _logger.Information("Config Changed: format: {InputFormat}, len: {fftLength}, Window: {WindowFunction}",
                        input.Format, FftLength, windowFunction);

                    Source = new BufferBlock<AudioBuffer>();
                    var action = new ActionBlock<AudioBuffer>(i =>
                    {
                        try
                        {
                            _time = i.Time;
                            OnDataReceived(i);
                        }
                        catch (Exception e)
                        {
                            _logger.Error("{e}", e);
                        }
                    });
                    var fftFormat = input.Format.WithSampleCount(FftLength);
                    var target = new AudioFlowSource(fftFormat, 4 * FftLength);
                    _logger.Information("Created, internal: {outFormat} fft={fftLength} {inFormat}", fftFormat, FftLength, input.Format);
                    target.LinkTo(action);
                    _link = input.SourceBlock.LinkTo(target);
                    Output.SourceBlock = Source;
                    Output.Format = input.Format;
                }
                catch (Exception e)
                {
                    _logger.Error("{e}", e.Message);
                }

                /*
                if (fftDirection == FFTDirection.Forwards)
                {
                    AudioFormat fftFormat = input.Format.WithSampleCount(fftLength);
                    // todo
                    
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
            */
            }
        }

        public void Post(AudioBuffer buf)
        {
            Source.Post(buf);
        }

        /*
        private void DoFFT_C2C(AudioBuffer input)
        {
            CopyInputComplex(input);
            using var pinIn = new PinnedArray<Complex>(_inputBufferComplex);
            using var output = new FftwArrayComplex(DFT.GetComplexBufferSize(pinIn.GetSize()));
            DFT.FFT(pinIn, output);
            CopyOutput(output);
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
*/
        public void Stop()
        {
            _link?.Dispose();
            _link = null;
        }

        protected AudioBuffer GetBuffer(int outLength)
        {
            var buf = AudioCore.Instance.BufferFactory.GetBuffer(outLength);
            buf.Time = _time;
            _time += new AudioTime(outLength, (double)outLength/Input.Format.SampleRate);
            return buf;
        }
        protected abstract bool ValidateInput(AudioLink input);
        protected abstract void OnDataReceived(AudioBuffer input);
        protected abstract void ResizeBuffers(int fftLength, int complexLength);

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }
    }

    public class ForwardFFT : BaseFFT
    {
        private readonly ILogger _logger = Log.ForContext<ForwardFFT>();

        private PinnedArray<double> _pinIn;
        private PinnedArray<Complex> _pinOut;

        public ForwardFFT()
        {
        }

        protected override bool ValidateInput(AudioLink input)
        {
            return input.Format.Channels == 1;
        }

        protected override void ResizeBuffers(int fftLength, int complexLength)
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();

            _pinIn = new PinnedArray<double>(fftLength);
            // var pinOut = new FftwArrayComplex(DFT.GetComplexBufferSize(_pinIn.GetSize()));
            // _logger.Information("calc pin out: {pinOut} my len {len}", pinOut.Length, complexLength);
            _pinOut = new PinnedArray<Complex>(complexLength);
        }

        private void CopyInputReal(AudioBuffer input)
        {
            for (int i = 0; i < FftLength; i++)
            {
                _pinIn[i] = input.Data[i] * Window[i];
            }
        }

        private void CopyOutputComplex()
        {
            var outLength = Input.Format.SampleCount;
            var outputBuffer = GetBuffer(outLength);
            var written = 0;
            for (int i = 0; i < FftLength / 2; i++)
            {
                if (written == outLength)
                {
                    Post(outputBuffer);
                    outputBuffer = GetBuffer(outLength);
                    written = 0;
                }

                outputBuffer.Data[written++] = (float)_pinOut[i].Real;
                outputBuffer.Data[written++] = (float)_pinOut[i].Imaginary;
            }

            Post(outputBuffer);
        }

        protected override void OnDataReceived(AudioBuffer input)
        {
            CopyInputReal(input);
            DFT.FFT(_pinIn, _pinOut);
            CopyOutputComplex();
        }

        public override void Dispose()
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();
            base.Dispose();
        }
    }

    public class BackwardFFT : BaseFFT
    {
        private readonly ILogger _logger = Log.ForContext<ForwardFFT>();
        private PinnedArray<Complex> _pinIn;
        private PinnedArray<double> _pinOut;

        protected override bool ValidateInput(AudioLink input)
        {
            return input.Format.Channels == 1;
        }

        protected override void ResizeBuffers(int fftLength, int complexLength)
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();

            _pinIn = new PinnedArray<Complex>(complexLength);
            _pinOut = new PinnedArray<double>(fftLength);
        }

        private void CopyInputComplex(AudioBuffer input)
        {
            _logger.Verbose("pinIn={pinInLen}, pinOut={pinOutLen}, input={inputLen}", _pinIn.Length, _pinOut.Length, input.Count);
            for (int i = 0; i < _pinIn.Length-1; i++)
            {
                _pinIn[i+1] = new Complex(input.Data[i * 2], input.Data[i * 2 + 1]);
            }
        }

        private void CopyOutputReal()
        {
            var outLength = Input.Format.SampleCount;
            var written = 0;
            var outputBuffer = GetBuffer(outLength);
            for (int b = 0; b < FftLength; b++)
            {
                if (written == outLength)
                {
                    Post(outputBuffer);
                    outputBuffer = GetBuffer(outLength);
                    written = 0;
                }

                outputBuffer.Data[written++] = (float)_pinOut[b] / FftLength;
            }
            Post(outputBuffer);
        }

        protected override void OnDataReceived(AudioBuffer input)
        {
            CopyInputComplex(input);
            DFT.IFFT(_pinIn, _pinOut);
            CopyOutputReal();
        }

        public override void Dispose()
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();
            base.Dispose();
        }
    }
}