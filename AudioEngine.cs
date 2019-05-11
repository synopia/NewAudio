using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VL.NewAudio
{
    public static class AudioEngine
    {
        private class DynamicOutput : ISampleProvider
        {
            public ISampleProvider Other { get; set; }
            public int Read(float[] buffer, int offset, int count)
            {
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

            public WaveFormat WaveFormat => Other?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        }

        private static WaveOutputDevice waveOutputDevice;
        private static IWavePlayer waveOut;

        private static DynamicOutput outputBridge = new DynamicOutput();
        private static string _error = "";
        private static WaveFormat _format;
        private static int _latency;

        public static void SetWaveOut(bool update, WaveOutputDevice device, ISampleProvider output, out string status, out string error, out WaveFormat waveFormatOut, out int latency, int requestedLatency = 300)
        {
            if( update ) 
            {
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                }

                waveOutputDevice = device;
                try
                {
                    var (newWaveOut, lat) = ((IWaveOutputFactory) device.Tag).Create(requestedLatency);
                    waveOut = newWaveOut;
                    var wave16 = new SampleToWaveProvider16(outputBridge);
                    var waveProvider = new MultiplexingWaveProvider(new IWaveProvider[]{wave16},2 );
                    waveProvider.ConnectInputToOutput(0, 0);
                    waveProvider.ConnectInputToOutput(0, 1);
                    waveOut.Init(waveProvider);
                    waveOut.Play();
                    _latency = lat;
                    _format = waveProvider.WaveFormat;
                    _error = "";
                }
                catch (Exception e)
                {
                    Log(e.ToString());
                    _error = e.Message;
                    waveOut = null;
                    waveOutputDevice = null;
                }
            }

            if (waveOut != null)
            {
                outputBridge.Other = output;
            }
            status = waveOut != null ? waveOut.PlaybackState.ToString() : "Uninitialized";
            error = _error;
            waveFormatOut = _format;
            latency = _latency;
        }

        private static StreamWriter log = File.CreateText("out.log"); 
        public static void Log(string line)
        {
            log.WriteLine(line);
            log.Flush();
        }
    }
}