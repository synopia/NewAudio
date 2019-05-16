using System;
using System.Diagnostics;
using System.Threading;
using NAudio.Wave;

namespace VL.NewAudio
{
    public class AudioThread : IDisposable
    {
        public AudioSampleBuffer Input;
        public AudioSampleBuffer Output;
        public int InternalLatency;
        public int BufferSize;

        private AudioThreadProcessor processor;

        public AudioSampleBuffer Update(AudioSampleBuffer input, out int latency,
            out float cpuUsage, out int bufferUnderRuns, int internalLatency = 100, int bufferSize = 512,
            bool reset = false)
        {
            bool hasChanges = Input != input
                              || InternalLatency != internalLatency
                              || reset;

            Input = input;
            InternalLatency = internalLatency;
            BufferSize = bufferSize;

            if (reset || BufferSize != bufferSize)
            {
                processor?.Dispose();
                processor = null;
            }

            if (processor == null)
            {
                processor = new AudioThreadProcessor(BufferSize);
            }

            processor.EnsureThreadIsRunning();

            if (hasChanges)
            {
                processor.Input = input;
                processor.RequestedLatency = InternalLatency;
                if (input != null)
                {
                    processor.Input = input;
                    processor.WaveFormat = input.WaveFormat;
                    Output = new AudioSampleBuffer(input.WaveFormat)
                    {
                        Processor = processor
                    };
                }
            }

            latency = processor.Latency;
            bufferUnderRuns = processor.BufferUnderRuns;
            cpuUsage = processor.CpuUsage;
            return Output;
        }

        public void Dispose()
        {
            processor.Dispose();
        }

        public class AudioThreadProcessor : IAudioProcessor, IDisposable, ISampleProvider
        {
            public AudioSampleBuffer Input;
            public int RequestedLatency;
            public bool Running;
            public int Latency;
            public int BufferUnderRuns => playBuffer.UnderRuns;
            public float CpuUsage;

            private BufferedSampleProvider playBuffer = new BufferedSampleProvider();
            private Thread playThread;
            private int bufferSize;

            public AudioThreadProcessor(int bufferSize)
            {
                this.bufferSize = bufferSize;
            }

            public WaveFormat WaveFormat
            {
                get { return playBuffer.WaveFormat; }
                set
                {
                    playBuffer.WaveFormat = value;
                    if (playBuffer.BufferDuration.Milliseconds < RequestedLatency * 4)
                    {
                        playBuffer.BufferDuration = TimeSpan.FromMilliseconds(RequestedLatency * 50);
                        playBuffer.ClearBuffer();
                    }
                }
            }

            public void EnsureThreadIsRunning()
            {
                if (playThread == null || !playThread.IsAlive)
                {
                    playThread?.Abort();
                    Running = true;
                    playThread = new Thread(RunInThread);
                    playThread.Start();
                    playBuffer = new BufferedSampleProvider();
                }
            }

            public int Read(float[] buffer, int offset, int count)
            {
                return playBuffer.Read(buffer, offset, count);
            }

            private void RunInThread()
            {
                float[] buffer = new float[bufferSize];
                var stopWatch = Stopwatch.StartNew();
                AudioEngine.Log("Starting AudioThread...");
                while (Running)
                {
                    if (playBuffer.IsValid && Input != null)
                    {
                        if (playBuffer.BufferedDuration.Milliseconds < RequestedLatency)
                        {
                            try
                            {
                                stopWatch.Stop();
                                var idleTime = stopWatch.ElapsedTicks;
                                stopWatch.Restart();

                                while (playBuffer.BufferedDuration.Milliseconds < RequestedLatency)
                                {
                                    Input.Read(buffer, 0, buffer.Length);
                                    playBuffer.AddSamples(buffer, 0, buffer.Length);
                                }

                                Latency = playBuffer.BufferedDuration.Milliseconds;
                                stopWatch.Stop();
                                var calcTime = stopWatch.ElapsedTicks;
                                stopWatch.Restart();

                                CpuUsage = (float) calcTime / (idleTime + calcTime);
                            }
                            catch (Exception e)
                            {
                                AudioEngine.Log(e);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }

                AudioEngine.Log("AudioThread terminated");
            }

            public void Dispose()
            {
                Running = false;
                Thread.Sleep(100);
                if (playThread.IsAlive)
                {
                    playThread.Abort();
                }
            }
        }
    }
}