using System;
using System.Threading;

namespace VL.NewAudio
{
    public class AudioThread
    {
        private AudioSampleBuffer input;
        private AudioSampleBuffer output;

        private int requestedLatency;
        private BufferedSampleProvider playBuffer;
        private Thread playThread;

        private int latency;
        private float cpuUsage;

        private bool running;

        public AudioSampleBuffer Update(AudioSampleBuffer input, int requestedLatency, out int latency,
            out float cpuUsage, out int bufferUnderRuns)
        {
            bool hasChanges = this.input != input || this.requestedLatency != requestedLatency;

            if (playThread == null || !playThread.IsAlive)
            {
                playThread?.Abort();
                running = true;
                playThread = new Thread(runInThread);
                playThread.Start();
                playBuffer = new BufferedSampleProvider();
            }

            if (hasChanges)
            {
                this.input = input;
                this.requestedLatency = requestedLatency;
                if (input != null)
                {
                    playBuffer.WaveFormat = input.WaveFormat;
                    if (playBuffer.BufferDuration.Milliseconds < requestedLatency * 4)
                    {
                        playBuffer.BufferDuration = TimeSpan.FromMilliseconds(requestedLatency * 5);
                        playBuffer.ClearBuffer();
                    }

                    output = new AudioSampleBuffer(input.WaveFormat)
                    {
                        Processor = playBuffer
                    };
                }
            }

            latency = this.latency;
            bufferUnderRuns = playBuffer.Overflows;
            cpuUsage = this.cpuUsage;
            return output;
        }

        private void runInThread()
        {
            float[] buffer = new float[128];
            while (running)
            {
                if (playBuffer.IsValid)
                {
                    if (playBuffer.BufferedDuration.Milliseconds < requestedLatency)
                    {
                        try
                        {
                            while (playBuffer.BufferedDuration.Milliseconds < requestedLatency)
                            {
                                input.Read(buffer, 0, buffer.Length);
                                playBuffer.AddSamples(buffer, 0, buffer.Length);
                            }

                            latency = playBuffer.BufferedDuration.Milliseconds;
                        }
                        catch (Exception e)
                        {
                            AudioEngine.Log(e.Message);
                            AudioEngine.Log(e.StackTrace);
                        }
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }

                Thread.Sleep(1);
            }
        }
    }
}