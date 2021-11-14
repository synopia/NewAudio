using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using FFTW.NET;
using NewAudio.Core;
using Serilog;

// ReSharper disable InconsistentNaming

namespace NewAudio.Nodes
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class FftParams : AudioParams
    {
        public AudioParam<int> FFTLength;
        public AudioParam<Utils.WindowFunction> WindowFunction;
    }

    public abstract class BaseFFT : AudioNode
    {
        protected double[] Window;

        private readonly BufferBlock<AudioDataMessage> _source;
        private AudioTime _time;
        private BatchBlock<AudioDataMessage> _batchBlock;
        private ActionBlock<AudioDataMessage[]> _processor;
        private IDisposable _link1;
        public FftParams Params { get; }
        protected BaseFFT()
        {
            InitLogger<BaseFFT>();
            Params = AudioParams.Create<FftParams>();
            Logger.Information("FFT created");
            _source = new BufferBlock<AudioDataMessage>();
        }

        public AudioLink Update(AudioLink input, int fftLength = 512,
            Utils.WindowFunction windowFunction = Utils.WindowFunction.None, int bufferSize = 1)
        {
            Params.WindowFunction.Value = windowFunction;
            Params.FFTLength.Value = (int)Utils.UpperPow2((uint)fftLength);
            PlayParams.Update(input, Params.HasChanged, bufferSize);

            return Update();
        }

        protected void Post(AudioDataMessage buf)
        {
            _source.Post(buf);
        }



        public override bool Play()
        {
            if (Params.FFTLength.Value > 0 && PlayParams.InputFormat.Value.Channels==1
                                           && PlayParams.InputFormat.Value.BufferSize > 0)
            {
                var input = PlayParams.Input.Value;
                var fftFormat = input.Format.WithSampleCount(Params.FFTLength.Value);
                Logger.Information("Created, internal: {OutFormat} fft={FftLength} {InFormat}", fftFormat,
                    Params.FFTLength.Value,
                    input.Format);

                Output.Format = input.Format;
                _batchBlock =
                    new BatchBlock<AudioDataMessage>(1 * Params.FFTLength.Value / input.Format.BufferSize);

                InitProcessor();
                
                _link1 = _batchBlock.LinkTo(_processor);
                Output.SourceBlock = _source;
                TargetBlock = _batchBlock;
                return true;
            }

            return false;
        }

        private void InitProcessor()
        {
            if (_processor != null)
            {
                Logger.Warning("ActionBlock != null!");
            }

            if (_batchBlock != null)
            {
                Logger.Warning("BatchBlock != null!");
            }

            if (_link1 != null)
            {
                Logger.Warning("link != null!");
                _link1.Dispose();
            }

            _processor = new ActionBlock<AudioDataMessage[]>(i =>
            {
                try
                {
                    _time = i[0].Time;
                    OnDataReceived(i);
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "ActionBlock");
                }
            });

            ResizeBuffers(Params.FFTLength.Value, (Params.FFTLength.Value - 1) / 2 + 2);
            Window = Utils.CreateWindow(Params.WindowFunction.Value, Params.FFTLength.Value);
            Output.SourceBlock = null;
        }
        public override void Stop()
        {
            _link1?.Dispose();
            _link1 = null;
            _processor?.Complete();
            _processor?.Completion.ContinueWith(t =>
            {
                _processor = null;
                Logger.Information("ActionBlock stopped, status={Status}", t.Status);
                return true;
            });
            TargetBlock = null;
            Output.SourceBlock = null;
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
            _time += new AudioTime(outLength, (double)outLength / PlayParams.Input.Value.Format.SampleRate);
            return buf;
        }


        public override string DebugInfo()
        {
            return
                $"curr={Params.FFTLength}, {_processor?.Completion.Status}, {_source?.Completion.Status}/ {_source?.Count}";
        }

        protected abstract void OnDataReceived(AudioDataMessage[] input);
        protected abstract void ResizeBuffers(int fftLength, int complexLength);
    }

    public class ForwardFFT : BaseFFT
    {
        public override string NodeName => "FFT";

        private PinnedArray<double> _pinIn;
        private PinnedArray<Complex> _pinOut;

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
            for (var i = 0; i < Params.FFTLength.Value; i++)
            {
                _pinIn[i] = input[block].Data[j++] * Window[i];
                if (input[block].Data.Length == j)
                {
                    block++;
                    j = 0;
                }
            }

            foreach (var message in input)
            {
                ArrayPool<float>.Shared.Return(message.Data);
            }
        }

        private void CopyOutputComplex()
        {
            var outLength = PlayParams.Input.Value.Format.SampleCount;
            var outputBuffer = GetBuffer(outLength);
            var written = 0;
            for (var i = 0; i < Params.FFTLength.Value / 2; i++)
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
        public override string NodeName => "iFFT";
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

            foreach (var message in input)
            {
                ArrayPool<float>.Shared.Return(message.Data);
            }
        }

        private void CopyOutputReal()
        {
            var outLength = PlayParams.Input.Value.Format.SampleCount;
            var written = 0;
            var outputBuffer = GetBuffer(outLength);
            for (var b = 0; b < Params.FFTLength.Value; b++)
            {
                if (written == outLength)
                {
                    Post(outputBuffer);
                    outputBuffer = GetBuffer(outLength);
                    written = 0;
                }

                outputBuffer.Data[written++] = (float)_pinOut[b] / Params.FFTLength.Value;
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