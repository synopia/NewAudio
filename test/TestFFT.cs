using System;
using System.Numerics;
using FFTW.NET;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class TestFFT
    {
        [Test]
        public void TestSin()
        {
            Complex[] input = new Complex[2048];
            Complex[] output = new Complex[2048];

            for (int i = 0; i < input.Length; i++)
            {
                input[i] = Math.Sin(i * 2 * Math.PI * 128 / input.Length);
            }

            using (var pinIn = new PinnedArray<Complex>(input))
            using (var pinOut = new PinnedArray<Complex>(output))
            {
                DFT.FFT(pinIn, pinOut);
//                DFT.IFFT(pinIn, pinOut);                
            }

            for (int i = 0; i < input.Length; i++)
            {
                if (i > 100)
                {
                    Console.WriteLine(output[i].Magnitude);
                    Console.WriteLine(Math.Sqrt(output[i].Imaginary * output[i].Imaginary +
                                                output[i].Real * output[i].Real));
                }
            }
        }
    }
}