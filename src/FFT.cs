using System;
using NAudio.Dsp;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class FFT
    {
        private int fftLength;
        private AudioSampleBuffer source;
        private AudioSampleBuffer output;
        private Complex[] fftBuffer;
        private int fftPos;
        private int m;

        private SpreadBuilder<float> spreadBuilder;
        private Spread<float> spread;

        public AudioSampleBuffer Update(AudioSampleBuffer source, int fftLength, out Spread<float> spread)
        {
            if (this.source != source)
            {
                this.source = source;
                output = new AudioSampleBuffer(source.WaveFormat);
                output.Update = (b, o, len) =>
                {
                    source.Read(b, o, len);
                    for (int i = 0; i < len; i += output.WaveFormat.Channels)
                    {
                        Add(b[o + i]);
                    }
                };
            }

            if (this.fftLength != fftLength)
            {
                this.fftLength = fftLength;
                if (IsPowerOfTwo(fftLength))
                {
                    m = (int) Math.Log(fftLength, 2.0);
                    fftBuffer = new Complex[fftLength];
                    fftPos = 0;
                    spreadBuilder = new SpreadBuilder<float>(fftLength * 2);
                }
                else
                {
                    fftBuffer = null;
                }
            }

            spread = this.spread;
            return output;
        }

        public static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        private void Add(float value)
        {
            if (fftBuffer != null)
            {
                fftBuffer[fftPos].X = (float) (value * FastFourierTransform.HammingWindow(fftPos, fftLength));
                fftBuffer[fftPos].Y = 0;
                fftPos++;
                if (fftPos >= fftLength)
                {
                    fftPos = 0;
                    FastFourierTransform.FFT(true, m, fftBuffer);
                    spreadBuilder.Clear();
                    for (int i = 0; i < fftLength; i++)
                    {
                        spreadBuilder.Add(fftBuffer[i].X);
                        spreadBuilder.Add(fftBuffer[i].Y);
                    }

                    spread = spreadBuilder.ToSpread();
                }
            }
        }
    }
}