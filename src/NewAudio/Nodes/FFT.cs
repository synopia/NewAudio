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
    public abstract class BaseFFT : IAudioNode<IFFTConfig>
    {
        private readonly ILogger _logger = Log.ForContext<BaseFFT>();
        protected double[] Window;

        private BufferBlock<AudioDataMessage> Source;
        private AudioTime _time;
        private BatchBlock<AudioDataMessage> _batchBlock;
        private ActionBlock<AudioDataMessage[]> _processor;
        private AudioNodeSupport<IFFTConfig> _support;

        public AudioParams AudioParams => _support.AudioParams;
        public IFFTConfig Config => _support.Config;
        public IFFTConfig LastConfig => _support.LastConfig;
        public AudioLink Output => _support.Output;

        protected BaseFFT()
        {
            Source = new BufferBlock<AudioDataMessage>();
            _support = new AudioNodeSupport<IFFTConfig>(this);

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
                }
            });
            Output.SourceBlock = Source;
        }

        public void OnConnect(AudioLink input)
        {
            var fftFormat = input.Format.WithSampleCount(Config.FFTLength);
            _logger.Information("Created, internal: {outFormat} fft={fftLength} {inFormat}", fftFormat, Config.FFTLength,
                input.Format);

            _batchBlock = new BatchBlock<AudioDataMessage>(1 * Config.FFTLength / input.Format.BufferSize);
            _support.AddLink(_batchBlock.LinkTo(_processor));
            _support.AddLink(input.SourceBlock.LinkTo(_batchBlock));

            Output.Format = input.Format;
        }

        public void OnDisconnect(AudioLink link)
        {
            _support.DisposeLinks();
        }

        public void Update(AudioLink input, int fftLength = 512,
            FFTUtils.WindowFunction windowFunction = FFTUtils.WindowFunction.None)
        {
            Config.WindowFunction = windowFunction;
            Config.Input = input;
            Config.FFTLength = (int)Utils.UpperPow2((uint)fftLength);;

            _support.Update();
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

        public bool IsInputValid(IFFTConfig next)
        {
            return true;
        }

        public void OnAnyChange()
        {
            ResizeBuffers(Config.FFTLength, (Config.FFTLength - 1) / 2 + 2);
            Window = FFTUtils.CreateWindow(Config.WindowFunction, Config.FFTLength);
        }

        public void OnStart()
        {
        }

        public void OnStop()
        {
        }

        protected abstract void OnDataReceived(AudioDataMessage[] input);
        protected abstract void ResizeBuffers(int fftLength, int complexLength);

        public virtual void Dispose()
        {
            OnStop();
            _support.Dispose();
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

        // protected override bool IsInputValid(AudioLink link)
        // {
            // return link.Format.Channels == 1;
        // }

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

        // protected override bool IsInputValid(AudioLink link)
        // {
            // return link.Format.Channels == 1;
        // }

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

        public override void Dispose()
        {
            _pinIn?.Dispose();
            _pinOut?.Dispose();
            base.Dispose();
        }
    }
}