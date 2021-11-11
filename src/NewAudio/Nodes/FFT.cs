using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using FFTW.NET;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;

namespace NewAudio.Nodes
{
    public class FftInitParams : AudioNodeInitParams
    {
        public AudioParam<int> FFTLength;
        public AudioParam<FFTUtils.WindowFunction> WindowFunction;

    }
    public class FftPlayParams : AudioNodePlayParams
    {
    }

    public abstract class BaseFFT : AudioNode<FftInitParams, FftPlayParams>
    {
        private readonly ILogger _logger = Log.ForContext<BaseFFT>();
        protected double[] Window;

        private BufferBlock<AudioDataMessage> Source;
        private AudioTime _time;
        private BatchBlock<AudioDataMessage> _batchBlock;
        private ActionBlock<AudioDataMessage[]> _processor;
        private IDisposable _link1;

        protected BaseFFT()
        {
            _logger.Information("FFT created");
            Source = new BufferBlock<AudioDataMessage>();
        }

        public AudioLink Update(AudioLink input, int fftLength = 512,
            FFTUtils.WindowFunction windowFunction = FFTUtils.WindowFunction.None, int bufferSize=4)
        {
            PlayParams.BufferSize.Value = bufferSize;
            InitParams.WindowFunction.Value = windowFunction;
            PlayParams.Input.Value = input;
            InitParams.FFTLength.Value = (int)Utils.UpperPow2((uint)fftLength);;

            return Update();
        }

        protected void Post(AudioDataMessage buf)
        {
            Source.Post(buf);
        }

        public override bool IsInitValid()
        {
            return InitParams.FFTLength.Value > 0;

        }
        public override bool IsPlayValid()
        {
            return PlayParams.Input.Value is { Format: { Channels: 1 } }
                   && PlayParams.Input.Value.Format.BufferSize > 0;
        }
        public override Task<bool> Init()
        {
            if (_processor != null)
            {
                _logger.Warning("ActionBlock != null!");
            }
            if (_batchBlock != null)
            {
                _logger.Warning("BatchBlock != null!");
            }

            if (_link1 != null)
            {
                _logger.Warning("link != null!");
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
            
            ResizeBuffers(InitParams.FFTLength.Value, (InitParams.FFTLength.Value - 1) / 2 + 2);
            Window = FFTUtils.CreateWindow(InitParams.WindowFunction.Value, InitParams.FFTLength.Value);
            Output.SourceBlock = null;
            
            return Task.FromResult(true);
        }
  

        public override bool Play()
        {
            var input = PlayParams.Input.Value;    
            var fftFormat = input.Format.WithSampleCount(InitParams.FFTLength.Value);
            _logger.Information("Created, internal: {outFormat} fft={fftLength} {inFormat}", fftFormat, InitParams.FFTLength,
                input.Format);

            Output.Format = input.Format;
            _batchBlock = new BatchBlock<AudioDataMessage>(1 * InitParams.FFTLength.Value / input.Format.BufferSize);

            _link1 = _batchBlock.LinkTo(_processor);
            Output.SourceBlock = Source;
            TargetBlock = _batchBlock;
            return true;
        }

        public override bool Stop()
        {
            TargetBlock = null;
            Output.SourceBlock = null;
            return true;
        }
        
        public override Task<bool> Free()
        {
            if (_processor == null)
            {
                _logger.Error("ActionBlock == null!");
            }
            if (_link1 == null)
            {
                _logger.Error("Link == null!");
            }
            _link1?.Dispose();
            _link1 = null;
            _processor?.Complete();
            return _processor?.Completion.ContinueWith(t =>
            {
                _processor = null;
                _logger.Information("ActionBlock stopped, status={status}", t.Status);
                return true;
            });
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
            return $"curr={InitParams.FFTLength}, {_processor?.Completion.Status}, {Source?.Completion.Status}/ {Source?.Count}";
        }

        protected abstract void OnDataReceived(AudioDataMessage[] input);
        protected abstract void ResizeBuffers(int fftLength, int complexLength);

    }

    public class ForwardFFT : BaseFFT
    {
        public override string NodeName => "FFT";
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
            for (var i = 0; i < InitParams.FFTLength.Value; i++)
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
            for (var i = 0; i < InitParams.FFTLength.Value / 2; i++)
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
            for (var b = 0; b < InitParams.FFTLength.Value; b++)
            {
                if (written == outLength)
                {
                    Post(outputBuffer);
                    outputBuffer = GetBuffer(outLength);
                    written = 0;
                }

                outputBuffer.Data[written++] = (float)_pinOut[b] / InitParams.FFTLength.Value;
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