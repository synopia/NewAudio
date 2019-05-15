using System;
using System.Diagnostics;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VL.NewAudio
{
    public class WaveOutput : IDisposable
    {
        public static WaveFormat InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public static WaveFormat SingleChannelFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

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

            public WaveFormat WaveFormat => InternalFormat;
        }

        private IWavePlayer waveOut;
        private static int latencyOut;
        private float cpuUsage;

        private readonly DynamicOutput outputBridge = new DynamicOutput();
        private string errorOut = "";
        private WaveFormat outputFormat;
        private BufferedWaveProvider playBuffer;
        private Thread bufferThread;

        private WaveOutputDevice selectedDevice;
        private int selectedRequestedLatency;
        private int bufferUnderruns;

        public void Update(WaveOutputDevice device, AudioSampleBuffer output, out string status,
            out string error, out WaveFormat waveFormatOut, out int latency, out float cpuUsage,
            out int bufferUnderruns, int sampleRate = 44100,
            int requestedLatency = 300, bool reset = false)
        {
            if (device?.Value != selectedDevice?.Value ||
                InternalFormat.SampleRate != sampleRate || requestedLatency != selectedRequestedLatency)
            {
                reset = true;
                selectedDevice = device;
                selectedRequestedLatency = requestedLatency;
            }

            if (reset)
            {
                if (waveOut != null)
                {
                    AudioEngine.Log("Stopping WaveOut...");
                    waveOut.Stop();
                    waveOut.Dispose();
                }

                bufferThread?.Abort();

                InternalFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
                SingleChannelFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

                if (device != null)
                {
                    AudioEngine.Log(
                        $"WaveOutput: Configuration changed, device={device.Value}, sampleRate={sampleRate}, requested latency={selectedRequestedLatency}");
                    try
                    {
                        waveOut = ((IWaveOutputFactory) device.Tag).Create(requestedLatency);
                        var wave16 = new SampleToWaveProvider16(outputBridge);
                        playBuffer = new BufferedWaveProvider(wave16.WaveFormat);
                        playBuffer.DiscardOnBufferOverflow = true;
                        bufferUnderruns = 0;
                        bufferThread = new Thread(() =>
                        {
                            byte[] buffer = new byte[512];
                            AudioEngine.Log("WaveOutput: Sound render thread started");
                            var stopwatch = Stopwatch.StartNew();
                            while (true)
                            {
                                if (playBuffer.BufferedBytes <
                                    wave16.WaveFormat.AverageBytesPerSecond * requestedLatency / 1000)
                                {
                                    try
                                    {
                                        stopwatch.Stop();
                                        var idle = stopwatch.ElapsedTicks;
                                        stopwatch.Restart();

                                        while (playBuffer.BufferedBytes <
                                               wave16.WaveFormat.AverageBytesPerSecond * requestedLatency / 1000)
                                        {
                                            wave16.Read(buffer, 0, 512);
                                            playBuffer.AddSamples(buffer, 0, 512);
                                        }

                                        latencyOut = playBuffer.BufferedBytes * 1000 /
                                                     wave16.WaveFormat.AverageBytesPerSecond;

                                        stopwatch.Stop();
                                        var calc = stopwatch.ElapsedTicks;
                                        stopwatch.Restart();

                                        this.cpuUsage = (float) calc / (idle + calc);
                                    }
                                    catch (Exception e)
                                    {
                                        this.bufferUnderruns++;
                                        AudioEngine.Log(e.Message);
                                        AudioEngine.Log(e.StackTrace);
                                    }
                                }
                            }
                        });
                        bufferThread.Start();
                        waveOut.Init(playBuffer);
                        waveOut.Play();
                        AudioEngine.Log("WaveOutput: Started");
                        outputFormat = wave16.WaveFormat;
                        errorOut = "";
                    }
                    catch (Exception e)
                    {
                        AudioEngine.Log(e.Message);
                        AudioEngine.Log(e.ToString());
                        errorOut = e.Message;
                        waveOut = null;
                    }
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
            cpuUsage = this.cpuUsage;
            bufferUnderruns = this.bufferUnderruns;
        }

        public void Dispose()
        {
            waveOut.Stop();
            waveOut.Dispose();
            bufferThread.Abort();
        }
    }
}