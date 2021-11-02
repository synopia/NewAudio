using System;
using System.Numerics;
using System.Threading.Tasks.Dataflow;
using FFTW.NET;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;

namespace NewAudio.Nodes
{


    public abstract class BaseFFT : BaseNode
    {
        private readonly ILogger _logger = Log.ForContext<BaseFFT>();
        protected int FftLength;
        private FFTUtils.WindowFunction _windowFunction;
        protected double[] Window;

        protected BufferBlock<AudioDataMessage> Source;

        private AudioTime _time;
        private BatchBlock<AudioDataMessage> _batchBlock;
        protected BaseFFT()
        {
            
            Source = new BufferBlock<AudioDataMessage>();
            
            var action = new ActionBlock<AudioDataMessage[]>(i =>
            {
                try
                {
                    _time = i[0].Time;
                    OnDataReceived(i);
                }
                catch (Exception e)
                {
                    _logger.Error("{e}", e);
                }
            });
            Output.SourceBlock = Source;


            OnConnect += link =>
            {
                
                _logger.Information("Config Changed: format: {InputFormat}, len: {fftLength}, Window: {WindowFunction}",
                    link.Format, FftLength, _windowFunction);

                var fftFormat = link.Format.WithSampleCount(FftLength);
                _logger.Information("Created, internal: {outFormat} fft={fftLength} {inFormat}", fftFormat, FftLength,
                    link.Format);

                _batchBlock = new BatchBlock<AudioDataMessage>(1 * FftLength / link.Format.BufferSize);
                AddLink(_batchBlock.LinkTo(action));
                AddLink(link.SourceBlock.LinkTo(_batchBlock));
                
                Output.Format = link.Format;
            };
        }

        public void ChangeSettings(AudioLink input, int fftLength=512, FFTUtils.WindowFunction windowFunction=FFTUtils.WindowFunction.None)
        {
            _logger.Information("Old format: {InputFormat}, len: {fftLength}, Window: {WindowFunction}",
                Input?.Format, FftLength, _windowFunction);


            _windowFunction = windowFunction;
            var oldLen = FftLength;
            FftLength = (int)Utils.UpperPow2((uint)fftLength);
            if (oldLen != FftLength)
            {
                ResizeBuffers(FftLength, (FftLength - 1) / 2 + 2);
                Window = FFTUtils.CreateWindow(_windowFunction, FftLength);
            }
            UpdateInput(input, true);

        }

        protected override void Start()
        {
            
        }

        protected override void Stop()
        {
            
        }

        protected void Post(AudioDataMessage buf)
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

        protected AudioDataMessage GetBuffer(int outLength)
        {
            var buf = new AudioDataMessage(Output.Format, outLength)
            {
                Time = _time
            };
            // todo
            _time += new AudioTime(outLength, (double)outLength/Input.Format.SampleRate);
            return buf;
        }
        protected abstract void OnDataReceived(AudioDataMessage[] input);
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

        protected override bool IsInputValid(AudioLink link)
        {
            return link.Format.Channels == 1;
        }

        protected override void ResizeBuffers(int fftLength, int complexLength)
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();

            _pinIn = new PinnedArray<double>(fftLength);
            _pinOut = new PinnedArray<Complex>(complexLength);
        }

        private void CopyInputReal(AudioDataMessage[] input)
        {
            var block = 0;
            var j = 0;
            for (int i = 0; i < FftLength; i++)
            {
                _pinIn[i] = input[block].Data[j++] * Window[i];
                if (input[block].Data.Length == j)
                {
                    block++;
                    j = 0;
                }
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

        protected override void OnDataReceived(AudioDataMessage[] input)
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

        protected override bool IsInputValid(AudioLink link)
        {
            return link.Format.Channels == 1;
        }

        protected override void ResizeBuffers(int fftLength, int complexLength)
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();

            _pinIn = new PinnedArray<Complex>(complexLength);
            _pinOut = new PinnedArray<double>(fftLength);
        }

        private void CopyInputComplex(AudioDataMessage[] input)
        {
            // _logger.Verbose("pinIn={pinInLen}, pinOut={pinOutLen}, input={inputLen}", _pinIn.Length, _pinOut.Length, input.Count);
            var block = 0;
            var j = 0;
            for (int i = 0; i < _pinIn.Length-1; i++)
            {
                _pinIn[i+1] = new Complex(input[block].Data[j], input[block].Data[j + 1]);
                j += 2;
                if (input[block].Data.Length == j)
                {
                    block++;
                    j = 0;
                }
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

        protected override void OnDataReceived(AudioDataMessage[] input)
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