using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VL.NewAudio
{
    public class WaveOutput
    {
        public static WaveFormat InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public static WaveFormat SingleChannelFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

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

        private IWavePlayer waveOut;
        private static int latencyOut;

        private readonly DynamicOutput outputBridge = new DynamicOutput();
        private string errorOut = "";
        private WaveFormat outputFormat;


        public void Update(bool reset, WaveOutputDevice device, AudioSampleBuffer output, out string status,
            out string error, out WaveFormat waveFormatOut, out int latency, int sampleRate = 44100,
            int requestedLatency = 300)
        {
            if (reset)
            {
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                }

                InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
                SingleChannelFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

                try
                {
                    waveOut = ((IWaveOutputFactory) device.Tag).Create(requestedLatency);
                    var wave16 = new SampleToWaveProvider16(outputBridge);
//                    var waveProvider = new MultiplexingWaveProvider(new IWaveProvider[] {wave16}, 2);
//                    waveProvider.ConnectInputToOutput(0, 0);
//                    waveProvider.ConnectInputToOutput(0, 1);
                    waveOut.Init(wave16);
                    waveOut.Play();
                    outputFormat = wave16.WaveFormat;
                    errorOut = "";
                }
                catch (Exception e)
                {
                    AudioEngine.Log(e.ToString());
                    errorOut = e.Message;
                    waveOut = null;
                }
            }

            if (waveOut != null)
            {
                outputBridge.Other = output;
                output.WaveFormat = outputFormat;
            }

            status = waveOut != null ? waveOut.PlaybackState.ToString() : "Uninitialized";
            error = errorOut;
            waveFormatOut = outputFormat;
            latency = latencyOut;
        }
    }
}