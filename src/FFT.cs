using System;
using System.Collections.Generic;
using FFTW.NET;
using NAudio.Dsp;
using VL.Lib.Collections;
using Complex = System.Numerics.Complex;

namespace VL.NewAudio
{
    public class FFT
    {
        public int FftLength;
        public AudioSampleBuffer Input;
        public AudioSampleBuffer Output;

        private FftProcessor processor;

        public AudioSampleBuffer Update(AudioSampleBuffer input, int fftLength, out Spread<float> spread)
        {
            bool hasChanges = fftLength != FftLength;

            FftLength = fftLength;

            if (hasChanges)
            {
                processor?.Dispose();
                processor = null;

                if (IsPowerOfTwo(fftLength))
                {
                    processor = new FftProcessor(fftLength);
                    processor.Input = input;

                    if (input != null)
                    {
                        Output = processor.Build();
                    }
                }
            }

            if (processor != null)
            {
                processor.Input = input;
            }

            spread = processor?.Spread ?? Spread<float>.Empty;
            return Output;
        }

        public static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        private class FftProcessor : IAudioProcessor, IDisposable
        {
            public AudioSampleBuffer Input;

            private int fftLength;
            private int fftPos;
            private int m;
            private Complex[] input;
            private Complex[] output;
            private PinnedArray<Complex> pinIn;
            private PinnedArray<Complex> pinOut;
            private SpreadBuilder<float> spreadBuilder;
            public Spread<float> Spread;

            public FftProcessor(int fftLength)
            {
                this.fftLength = fftLength;
                m = (int) Math.Log(fftLength, 2.0);
                input = new Complex[fftLength];
                output = new Complex[fftLength];
                pinIn = new PinnedArray<Complex>(input);
                pinOut = new PinnedArray<Complex>(output);
                fftPos = 0;
                spreadBuilder = new SpreadBuilder<float>(fftLength / 2);
            }

            public List<AudioSampleBuffer> GetInputs()
            {
                return new List<AudioSampleBuffer> {Input};
            }

            public AudioSampleBuffer Build()
            {
                return new AudioSampleBuffer(Input.WaveFormat)
                {
                    Processor = this
                };
            }


            private void Add(float value)
            {
                input[fftPos] = value * FastFourierTransform.HammingWindow(fftPos, fftLength);
                fftPos++;
                if (fftPos >= fftLength)
                {
                    fftPos = 0;
                    DFT.FFT(pinIn, pinOut);
                    spreadBuilder.Clear();
                    for (int i = 1; i < fftLength / 2; i++)
                    {
                        spreadBuilder.Add((float) (2.0 * pinOut[i].Magnitude * i / fftLength));
                    }

                    Spread = spreadBuilder.ToSpread();
                }
            }

            public int Read(float[] buffer, int offset, int count)
            {
                if (Input != null)
                {
                    var l = Input.Read(buffer, offset, count);
                    for (int i = 0; i < l; i += Input.WaveFormat.Channels)
                    {
                        Add(buffer[offset + i]);
                    }

                    return l;
                }

                Spread = null;
                return count;
            }

            public void Dispose()
            {
                pinIn.Dispose();
                pinOut.Dispose();
            }
        }
    }
}