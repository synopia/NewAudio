using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VL.Lib.Animation;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public static class AudioEngine
    {
        private class DynamicOutput : ISampleProvider
        {
            public ISampleProvider Other { get; set; }

            public int Read(float[] buffer, int offset, int count)
            {
                latencyOut = count * 1000 / WaveFormat.SampleRate;
                if (Other != null)
                {
                    return Other.Read(buffer, offset, count);
                }
                else
                {
                    Array.Clear(buffer, offset, count);
                    return count;
                }
            }

            public WaveFormat WaveFormat => InternalFormat;
        }

        private static IWavePlayer waveOut;
        private static IWaveIn waveIn;

        private static readonly DynamicOutput outputBridge = new DynamicOutput();
        private static AudioSampleBuffer inputBridge;
        private static BufferedWaveProvider bufferedWave;
        private static string errorOut = "";
        private static string errorIn = "";
        private static WaveFormat outputFormat;
        private static WaveFormat inputFormat;
        private static int latencyOut;

        public static WaveFormat InternalFormat;

        public static AudioSampleBuffer WaveInput(bool update, WaveInputDevice device, out string status,
            out string error,
            out WaveFormat waveFormat, int latency = 300)
        {
            if (update)
            {
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                }

                try
                {
                    waveIn = ((IWaveInputFactory) device.Tag).Create(latency);

                    waveIn.DataAvailable += (s, a) => { bufferedWave.AddSamples(a.Buffer, 0, a.BytesRecorded); };
                    bufferedWave = new BufferedWaveProvider(waveIn.WaveFormat);
                    var waveProvider = new MultiplexingWaveProvider(new IWaveProvider[] {bufferedWave}, 1);
                    waveProvider.ConnectInputToOutput(0, 0);
                    var sampleProvider = new WaveToSampleProvider(waveProvider);
                    inputFormat = sampleProvider.WaveFormat;
                    waveIn.StartRecording();
                    inputBridge = new AudioSampleBuffer();
                    inputBridge.Update = (b, o, l) => { sampleProvider.Read(b, o, l); };
                }
                catch (Exception e)
                {
                    Log(e.ToString());
                    errorIn = e.Message;
                    waveIn = null;
                }
            }

            error = errorIn;
            status = waveIn != null ? "Recording" : "Uninitialized";
            waveFormat = inputFormat;
            return inputBridge;
        }

        public static void WaveOutput(bool update, WaveOutputDevice device, AudioSampleBuffer output, out string status,
            out string error, out WaveFormat waveFormatOut, out int latency, int sampleRate = 44100,
            int requestedLatency = 300)
        {
            if (update)
            {
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                }

                InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

                try
                {
                    waveOut = ((IWaveOutputFactory) device.Tag).Create(requestedLatency);
                    var wave16 = new SampleToWaveProvider16(outputBridge);
                    var waveProvider = new MultiplexingWaveProvider(new IWaveProvider[] {wave16}, 2);
                    waveProvider.ConnectInputToOutput(0, 0);
                    waveProvider.ConnectInputToOutput(0, 1);
                    waveOut.Init(waveProvider);
                    waveOut.Play();
                    outputFormat = waveProvider.WaveFormat;
                    errorOut = "";
                }
                catch (Exception e)
                {
                    Log(e.ToString());
                    errorOut = e.Message;
                    waveOut = null;
                }
            }

            if (waveOut != null)
            {
                outputBridge.Other = output;
            }

            status = waveOut != null ? waveOut.PlaybackState.ToString() : "Uninitialized";
            error = errorOut;
            waveFormatOut = outputFormat;
            latency = latencyOut;
        }


        public static Spread<float> SolveODEEuler(float dt, float t, int len, Spread<float> x,
            Func<float, Spread<float>, Spread<float>> f)
        {
            if (x.Count != len)
            {
                float[] xx = new float[len];
                x = xx.ToSpread();
            }

            var k = f(t, x);
            var r = new SpreadBuilder<float>();
            for (int i = 0; i < x.Count; i++)
            {
                r.Add(x[i] + dt * k[i]);
            }

            return r.ToSpread();
        }

        public static Spread<float> SolveODERK4(IFrameClock clock, float t, int len, Spread<float> x,
            Func<float, Spread<float>, Spread<float>> f)
        {
            var dt = (float) clock.TimeDifference;
            var k1 = f(t, x);
            var yi = new SpreadBuilder<float>();
            for (int i = 0; i < x.Count; i++)
            {
                yi.Add(x[i] + k1[i] * dt / 2.0);
            }

            var k2 = f(t + dt / 2, yi.ToSpread());
            for (int i = 0; i < x.Count; i++)
            {
                yi[i] = x[i] + k2[i] * dt / 2;
            }

            var k3 = f(t + dt / 2, yi.ToSpread());
            for (int i = 0; i < x.Count; i++)
            {
                yi[i] = x[i] + k3[i] * dt / 2;
            }

            var k4 = f(t + dt / 2, yi.ToSpread());
            var r = new SpreadBuilder<float>();
            for (int i = 0; i < x.Count; i++)
            {
                r.Add(x[i] + dt * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]) / 6);
            }

            return r.ToSpread();
        }

        private static StreamWriter log = File.CreateText("out.log");

        public static void Log(string line)
        {
            log.WriteLine(line);
            log.Flush();
        }
    }
}