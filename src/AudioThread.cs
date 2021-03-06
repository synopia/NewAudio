using System;
using System.Collections.Generic;
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
        private bool RunWithoutOutput;

        public BufferedSampleProvider PlayBuffer => processor?.PlayBuffer;

        public AudioSampleBuffer Update(AudioSampleBuffer input, out int latency,
            out float cpuUsage, out int bufferUnderRuns, bool runWithoutOutput, int internalLatency = 100,
            int bufferSize = 512,
            bool reset = false)
        {
            bool hasChanges = Input != input
                              || BufferSize != bufferSize
                              || InternalLatency != internalLatency
                              || RunWithoutOutput != runWithoutOutput
                              || reset;

            Input = input;
            InternalLatency = internalLatency;
            BufferSize = bufferSize;
            RunWithoutOutput = runWithoutOutput;

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
                processor.RunWithoutOutput = RunWithoutOutput;
                if (input != null)
                {
                    processor.Input = input;
                    processor.WaveFormat = input.WaveFormat;
                    if (!RunWithoutOutput)
                    {
                        Output = new AudioSampleBuffer(input.WaveFormat)
                        {
                            Processor = processor
                        };
                    }
                    else
                    {
                        Output = null;
                    }
                }
                else
                {
                    processor.ClearBuffer();
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
            public ISampleProvider Input;
            public int RequestedLatency;
            public bool Running;
            public int Latency;
            public int BufferUnderRuns => playBuffer.UnderRuns;
            public float CpuUsage;
            public bool RunWithoutOutput;

            private BufferedSampleProvider playBuffer = new BufferedSampleProvider();
            private Thread playThread;
            private int bufferSize;

            public BufferedSampleProvider PlayBuffer => playBuffer;
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

            public void ClearBuffer()
            {
                playBuffer?.ClearBuffer();
            }
            public List<AudioSampleBuffer> GetInputs()
            {
                if (Input is AudioSampleBuffer)
                {
                    return new List<AudioSampleBuffer> {(AudioSampleBuffer) Input};
                }

                return AudioSampleBuffer.EmptyList;
            }

            public void EnsureThreadIsRunning()
            {
                if (playThread == null)
                {
                    playThread?.Abort();
                    Running = true;
                    playThread = new Thread(RunInThread);
                    playBuffer = new BufferedSampleProvider();
                    playThread.Start();
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
                var clock = Stopwatch.StartNew();
                var lastElapsed = 0.0;
                AudioEngine.Log($"Starting AudioThread {GetHashCode()}...");
                try
                {
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
                                        Input?.Read(buffer, 0, buffer.Length);
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
                            else
                            {
                                if (RunWithoutOutput)
                                {
                                    var elapsed = clock.Elapsed.TotalSeconds;
                                    var delta = elapsed - lastElapsed;
                                    lastElapsed = elapsed;
                                    playBuffer.Advance(TimeSpan.FromSeconds(delta));
                                    Thread.Sleep(RequestedLatency);
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                }
                catch (Exception e)
                {
                    AudioEngine.Log(e);
                }

                AudioEngine.Log($"AudioThread {GetHashCode()} terminated");
            }

            public void Dispose()
            {
                Running = false;
                if (playThread.IsAlive)
                {
                    playThread.Abort();
                }
            }
        }
    }
}