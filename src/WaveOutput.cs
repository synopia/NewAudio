using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VL.NewAudio
{
    public class WaveOutput : IDisposable
    {
        public static WaveFormat InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public static WaveFormat SingleChannelFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

        private IWavePlayer waveOut;

        public WaveOutputDevice Device;
        public AudioSampleBuffer Input;
        public int RequestedLatency;
        public int SampleRate;
        public WaveFormat OutputFormat;

        private AudioThread.AudioThreadProcessor processor;

        public void Update(WaveOutputDevice device, AudioSampleBuffer input, out string status,
            out WaveFormat waveFormat, out int latency, out float cpuUsage,
            out int bufferUnderRuns, int sampleRate = 44100,
            int requestedLatency = 300, bool reset = false)
        {
            bool hasDeviceChanges = device?.Value != Device?.Value
                                    || sampleRate != SampleRate
                                    || reset;
            Device = device;
            Input = input;
            SampleRate = sampleRate;
            RequestedLatency = requestedLatency;

            if (reset)
            {
                if (waveOut != null)
                {
                    AudioEngine.Log("Stopping WaveOut...");
                    waveOut.Stop();
                    waveOut.Dispose();
                }

                processor?.Dispose();
                processor = null;
            }

            if (processor == null)
            {
                processor = new AudioThread.AudioThreadProcessor();
            }

            processor.EnsureThreadIsRunning();

            if (hasDeviceChanges)
            {
                InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
                SingleChannelFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

                processor.RequestedLatency = RequestedLatency;
                processor.WaveFormat = InternalFormat;

                if (device != null)
                {
                    AudioEngine.Log(
                        $"WaveOutput: Configuration changed, device={device.Value}, sampleRate={sampleRate}, requested latency={RequestedLatency}");
                    try
                    {
                        waveOut = ((IWaveOutputFactory) device.Tag).Create(requestedLatency);
                        var wave16 = new SampleToWaveProvider16(processor);
                        waveOut.Init(wave16);
                        waveOut.Play();
                        AudioEngine.Log("WaveOutput: Started");
                        OutputFormat = wave16.WaveFormat;
                    }
                    catch (Exception e)
                    {
                        AudioEngine.Log(e);
                    }
                }
            }

            processor.Input = Input;
            processor.RequestedLatency = RequestedLatency;

            status = waveOut != null ? waveOut.PlaybackState.ToString() : "Uninitialized";
            waveFormat = OutputFormat;
            latency = processor.Latency;
            cpuUsage = processor.CpuUsage;
            bufferUnderRuns = processor.BufferUnderRuns;
        }

        public void Dispose()
        {
            waveOut?.Stop();
            waveOut?.Dispose();
            processor?.Dispose();
        }
    }
}