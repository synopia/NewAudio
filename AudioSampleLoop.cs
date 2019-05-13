using System;
using NAudio.Wave;
using VL.Lib.Animation;

namespace VL.NewAudio
{
    public class AudioSampleFrameClock : IFrameClock
    {
        private Time frameTime;
        private bool initialized;

        public Time Time => frameTime;
        public double TimeDifference { get; private set; }

        public void SetFrameTime(Time frameTime)
        {
            if (initialized)
            {
                TimeDifference = frameTime.Seconds - this.frameTime.Seconds;
            }
            else
            {
                TimeDifference = 0;
            }

            this.frameTime = frameTime;
            initialized = true;
        }

        public void IncrementTime(double diff)
        {
            frameTime += diff;
            TimeDifference = diff;
            initialized = true;
        }

        public void Restart()
        {
            initialized = false;
            frameTime = 0;
            TimeDifference = 0;
        }

        public IObservable<FrameTimeMessage> GetTicks()
        {
            throw new NotImplementedException();
        }
    }

    public class AudioSampleLoop<TState> where TState : class
    {
        private TState state;
        private readonly AudioSampleFrameClock sampleClock = new AudioSampleFrameClock();
        private AudioSampleBuffer buffer = new AudioSampleBuffer(WaveOutput.SingleChannelFormat);
        private Func<TState, float[], int, float[], int, TState> updateFunction;
        private float[] inputBuffer;
        private int oversample;
        private Decimator[] decimators;
        private float[] oversampleBuffer;
        private float[] oversampleBuffer2;

        public AudioSampleBuffer Update(
            bool reset,
            AudioSampleBuffer input,
            Func<IFrameClock, TState> create, Func<TState, float[], int, float[], int, TState> update,
            int outputChannels = 1, int oversample = 1)
        {
            if (reset)
            {
                state = create(sampleClock);
            }

            if (buffer.WaveFormat.Channels != outputChannels)
            {
                buffer = new AudioSampleBuffer(
                    WaveFormat.CreateIeeeFloatWaveFormat(WaveOutput.InternalFormat.SampleRate, outputChannels));
                this.oversample = 0;
            }

            if (oversample != this.oversample)
            {
                this.oversample = oversample;
                if (oversample > 1)
                {
                    decimators = new Decimator[outputChannels];
                    oversampleBuffer = new float[oversample * outputChannels];
                    oversampleBuffer2 = new float[oversample];
                    for (int i = 0; i < outputChannels; i++)
                    {
                        decimators[i] = new Decimator(oversample, oversample);
                    }
                }
            }

            if (update != updateFunction)
            {
                buffer.Update = (b, offset, count) =>
                {
                    try
                    {
                        if (input != null)
                        {
                            var inputSamples = count * input.WaveFormat.Channels / outputChannels;

                            if (inputBuffer == null || inputBuffer.Length != inputSamples)
                            {
                                inputBuffer = new float[inputSamples];
                            }

                            input.Read(inputBuffer, offset, inputSamples);
                        }

                        if (oversample == 1)
                        {
                            var increment = 1.0 / WaveOutput.InternalFormat.SampleRate;
                            var inputChannels = input?.WaveFormat?.Channels ?? 0;
                            for (int i = 0; i < count / outputChannels; i++)
                            {
                                state = update(state, inputBuffer, i * inputChannels, b, i * outputChannels);
                                sampleClock.IncrementTime(increment);
                            }
                        }
                        else
                        {
                            var increment = 1.0 / WaveOutput.InternalFormat.SampleRate / oversample;
                            var inputChannels = input?.WaveFormat?.Channels ?? 0;
                            for (int i = 0; i < count / outputChannels; i++)
                            {
                                for (int j = 0; j < oversample; j++)
                                {
                                    state = update(state, inputBuffer, i * inputChannels, oversampleBuffer,
                                        j * outputChannels);
                                    sampleClock.IncrementTime(increment);
                                }

                                if (outputChannels == 1)
                                {
                                    b[i * outputChannels] = decimators[0].Process(oversampleBuffer);
                                }
                                else
                                {
                                    for (int j = 0; j < outputChannels; j++)
                                    {
                                        for (int k = 0; k < oversample; k++)
                                        {
                                            oversampleBuffer2[k] = oversampleBuffer[k * outputChannels + j];
                                        }

                                        b[i * outputChannels + j] = decimators[j].Process(oversampleBuffer2);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AudioEngine.Log(e.Message);
                        AudioEngine.Log(e.StackTrace);
                    }
                };

                updateFunction = update;
            }

            return buffer;
        }
    }
}