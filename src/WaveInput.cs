using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VL.NewAudio
{
    public class WaveInput : IDisposable
    {
        private IWaveIn waveIn;

        private AudioSampleBuffer output;

        public WaveFormat OutputFormat;
        public WaveInputDevice Device;
        public int DriverLatency;
        public int InternalLatency;
        public int BufferSize;

        private AudioThread.AudioThreadProcessor processor;
        private BufferedWaveProvider bufferedWave;
        private WaveToSampleProvider sampleProvider;

        public AudioSampleBuffer Update(WaveInputDevice device, out string status,
            out WaveFormat waveFormat, out int latency, out float cpuUsage, out int bufferUnderRuns,
            int driverLatency = 150, int internalLatency = 8, int bufferSize = 512, bool reset = false)
        {
            bool hasDeviceChanged = Device?.Value != device?.Value
                                    || DriverLatency != driverLatency
                                    || BufferSize != bufferSize
                                    || reset;

            Device = device;
            DriverLatency = driverLatency;
            InternalLatency = internalLatency;
            BufferSize = bufferSize;

            if (hasDeviceChanged)
            {
                processor?.Dispose();
                processor = null;

                if (waveIn != null)
                {
                    AudioEngine.Log("Stopping WaveIn...");
                    waveIn.StopRecording();
                    waveIn.Dispose();
                }
            }


            if (processor == null)
            {
                processor = new AudioThread.AudioThreadProcessor(bufferSize);
            }

            processor.EnsureThreadIsRunning();
            processor.RequestedLatency = internalLatency;

            if (hasDeviceChanged)
            {
                if (device != null)
                {
                    AudioEngine.Log(
                        $"WaveInput: Configuration changed, device={device.Value}, requested latency={DriverLatency}");
                    try
                    {
                        waveIn = ((IWaveInputFactory) device.Tag).Create(DriverLatency);
                        bufferedWave = new BufferedWaveProvider(waveIn.WaveFormat);
                        bufferedWave.DiscardOnBufferOverflow = true;
                        sampleProvider = new WaveToSampleProvider(bufferedWave);
                        OutputFormat = sampleProvider.WaveFormat;
                        processor.WaveFormat = OutputFormat;
                        processor.Input = sampleProvider;

                        waveIn.DataAvailable += (s, a) => { bufferedWave.AddSamples(a.Buffer, 0, a.BytesRecorded); };
                        waveIn.StartRecording();
                        AudioEngine.Log("WaveInput: Started");

                        output = new AudioSampleBuffer(OutputFormat)
                        {
                            Processor = processor
                        };
                    }
                    catch (Exception e)
                    {
                        AudioEngine.Log(e);
                        waveIn = null;
                    }
                }
            }


            status = waveIn != null ? "Recording" : "Uninitialized";
            waveFormat = OutputFormat;
            latency = processor.Latency;
            cpuUsage = processor.CpuUsage;
            bufferUnderRuns = processor.BufferUnderRuns;
            return output;
        }

        public void Dispose()
        {
            processor?.Dispose();
            waveIn?.StopRecording();
            waveIn?.Dispose();
        }
    }
}