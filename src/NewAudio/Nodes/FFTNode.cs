using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using VL.NewAudio.Dsp;
using VL.NewAudio.Nodes;
using VL.Lang;
using VL.Lib.Collections;
using VL.NewAudio.Processor;

namespace VL.NewAudio.Nodes
{
    public class FftNode : AudioProcessorNode<MonitorProcessor>
    {
        private readonly BaseFft _fft;
        private int _fftSize;
        private float[]? _data;
        private bool _disposedValue;
        public AudioMath.WindowFunction WindowFunction { get; set; } = AudioMath.WindowFunction.None;

        public int FftSize
        {
            get => _fftSize;
            set
            {
                var newSize = (int)AudioMath.UpperPow2((uint)value);
                if (newSize != _fftSize)
                {
                    _fftSize = newSize;
                    if (_data != null)
                    {
                        ArrayPool<float>.Shared.Return(_data);
                    }

                    _data = ArrayPool<float>.Shared.Rent(_fftSize);
                    Processor.BufferSize = _fftSize * 2;
                }
            }
        }

        public Spread<float> Buffer { get; set; } = Spread<float>.Empty;

        public override bool IsEnabled => IsEnable;

        public FftNode(bool forward) : base(new MonitorProcessor())
        {
            _fft = forward ? new ForwardFft() : new BackwardFft();
        }

        public void FillBuffer()
        {
            if (!IsEnable)
            {
                return;
            }

            var ringBuffer = Processor.RingBuffers[0];
            if (ringBuffer.AvailableRead >= _fftSize)
            {
                ringBuffer.Read(_data!, _fftSize);
                Task.Run(() =>
                {
                    _fft.DoFft(_data!, _fftSize, WindowFunction);
                    Buffer = Spread.Create(_data);
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _fft.Dispose();
                    if (_data != null)
                    {
                        ArrayPool<float>.Shared.Return(_data);
                    }
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}