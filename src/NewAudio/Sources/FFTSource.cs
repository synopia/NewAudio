using System.Threading.Tasks;
using VL.Lib.Collections;
using VL.NewAudio.Dsp;
using VL.NewAudio.Nodes;

namespace VL.NewAudio.Sources
{
    public class FFTSource: BaseBufferOutSource
    {
        private readonly BaseFft _fft;
        private int _fftSize;

        public AudioMath.WindowFunction WindowFunction { get; set; } = AudioMath.WindowFunction.None;

        public int FftSize
        {
            get => _fftSize;
            set
            {
                _fftSize = (int)AudioMath.UpperPow2((uint)value);
                BufferSize = _fftSize;
            }
        }
        public Spread<float> Buffer { get; set; } = Spread<float>.Empty;

        public FFTSource(bool forward)
        {
            _fft = forward ? new ForwardFft() : new BackwardFft();
        }

        protected override void OnDataReady(float[] data)
        {
            // Task.Run(() =>
            // {
                _fft.DoFft(data, _fftSize, WindowFunction);
                Buffer = Spread.Create(data);                    
            // });
        }
    }
}