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
    public interface IFFTConfig : IAudioNodeConfig
    {
        int FFTLength { get; set; }
        FFTUtils.WindowFunction WindowFunction { get; set; }
        
    }
    public abstract class BaseFFT : AudioNode<IFFTConfig>
    {
        private readonly ILogger _logger = Log.ForContext<BaseFFT>();
        protected double[] Window;

        private BufferBlock<AudioDataMessage> Source;
        private AudioTime _time;
        private BatchBlock<AudioDataMessage> _batchBlock;
        private ActionBlock<AudioDataMessage[]> _processor;

        protected BaseFFT()
        {
            Source = new BufferBlock<AudioDataMessage>();

            _processor = new ActionBlock<AudioDataMessage[]>(i =>
            {
                try
                {
                    _time = i[0].Time;
                    OnDataReceived(i);
                }
                catch (Exception e)
                {
                    _logger.Error("{e}", e);
                    HandleError(e);
                }
            });
            Output.SourceBlock = Source;
        }

        protected override void OnConnect(AudioLink input)
        {
            Output.Format = input.Format;
        }

        public AudioLink Update(AudioLink input, int fftLength = 512,
            FFTUtils.WindowFunction windowFunction = FFTUtils.WindowFunction.None)
        {
            Config.WindowFunction = windowFunction;
            Config.Input = input;
            Config.FFTLength = (int)Utils.UpperPow2((uint)fftLength);;

            return Update();
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
            _time += new AudioTime(outLength, (double)outLength / Config.Input.Format.SampleRate);
            return buf;
        }

        protected override bool IsInputValid(IFFTConfig next)
        {
            return next.FFTLength > 0 && next.Input.Format.Channels == 1;
        }

        public override string DebugInfo()
        {
            return $"curr={Config.FFTLength}, {_processor?.Completion.Status}, {Source?.Completion.Status}/ {Source?.Count}";
        }

        protected override void OnAnyChange()
        {
            DisposeLinks();
            if (_batchBlock != null)
            {
                _batchBlock.Completion.ContinueWith(task =>
                {
                    DoResize();
                });
                _batchBlock.Complete();
            }
            else
            {
                DoResize();
            }
            
        }

        private void DoResize()
        {
            var input = Config.Input;
            ResizeBuffers(Config.FFTLength, (Config.FFTLength - 1) / 2 + 2);
            Window = FFTUtils.CreateWindow(Config.WindowFunction, Config.FFTLength);
            
            var fftFormat = input.Format.WithSampleCount(Config.FFTLength);
            _logger.Information("Created, internal: {outFormat} fft={fftLength} {inFormat}", fftFormat, Config.FFTLength,
                input.Format);

            _batchBlock = new BatchBlock<AudioDataMessage>(1 * Config.FFTLength / input.Format.BufferSize);
            AddLink(_batchBlock.LinkTo(_processor));
            AddLink(input.SourceBlock.LinkTo(_batchBlock));


        }

        protected abstract void OnDataReceived(AudioDataMessage[] input);
        protected abstract void ResizeBuffers(int fftLength, int complexLength);

    }

    public class ForwardFFT : BaseFFT
    {
        private readonly ILogger _logger = Log.ForContext<ForwardFFT>();

        private PinnedArray<double> _pinIn;
        private PinnedArray<Complex> _pinOut;

        public ForwardFFT()
        {
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
            for (var i = 0; i < Config.FFTLength; i++)
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
            var outLength = Config.Input.Format.SampleCount;
            var outputBuffer = GetBuffer(outLength);
            var written = 0;
            for (var i = 0; i < Config.FFTLength / 2; i++)
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

    public class BackwardFFT : BaseFFT
    {
        private readonly ILogger _logger = Log.ForContext<ForwardFFT>();
        private PinnedArray<Complex> _pinIn;
        private PinnedArray<double> _pinOut;

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
            for (var i = 0; i < _pinIn.Length - 1; i++)
            {
                _pinIn[i + 1] = new Complex(input[block].Data[j], input[block].Data[j + 1]);
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
            var outLength = Config.Input.Format.SampleCount;
            var written = 0;
            var outputBuffer = GetBuffer(outLength);
            for (var b = 0; b < Config.FFTLength; b++)
            {
                if (written == outLength)
                {
                    Post(outputBuffer);
                    outputBuffer = GetBuffer(outLength);
                    written = 0;
                }

                outputBuffer.Data[written++] = (float)_pinOut[b] / Config.FFTLength;
            }

            Post(outputBuffer);
        }

        protected override void OnDataReceived(AudioDataMessage[] input)
        {
            CopyInputComplex(input);
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