using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VL.NewAudio
{
    public class WaveOutput : BaseAudioNode, IDisposable
    {
        public static WaveFormat InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public static WaveFormat SingleChannelFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

        private IWavePlayer waveOut;

        public WaveOutputDevice Device;
        public AudioSampleBuffer Input;
        public int DriverLatency;
        public int InternalLatency;
        public int BufferSize;

        public int SampleRate;
        public WaveFormat OutputFormat;

        private AudioThread.AudioThreadProcessor processor;

        public void Update(WaveOutputDevice device, AudioSampleBuffer input, out string status,
            out WaveFormat waveFormat, out int latency, out float cpuUsage,
            out int bufferUnderRuns, int sampleRate = 44100,
            int driverLatency = 200, int internalLatency = 300, int bufferSize = 512, bool reset = false)
        {
            bool hasDeviceChanged = device?.Value != Device?.Value
                                    || sampleRate != SampleRate
                                    || driverLatency != DriverLatency
                                    || bufferSize != BufferSize
                                    || reset;
            Device = device;
            Input = input;
            SampleRate = sampleRate;
            InternalLatency = internalLatency;
            DriverLatency = driverLatency;
            BufferSize = bufferSize;

            if (reset || hasDeviceChanged)
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
                processor = new AudioThread.AudioThreadProcessor(BufferSize);
            }

            processor.EnsureThreadIsRunning();

            if (hasDeviceChanged)
            {
                InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
                SingleChannelFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

                processor.RequestedLatency = InternalLatency;
                processor.WaveFormat = InternalFormat;

                if (device != null)
                {
                    AudioEngine.Log(
                        $"WaveOutput: Configuration changed, device={device.Value}, sampleRate={sampleRate}, latency={InternalLatency}");
                    try
                    {
                        waveOut = ((IWaveOutputFactory) device.Tag).Create(DriverLatency);
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
            processor.RequestedLatency = InternalLatency;

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