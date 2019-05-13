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
        private Decimator decimator;

        public AudioSampleBuffer Update(
            bool reset,
            AudioSampleBuffer input,
            Func<IFrameClock, TState> create, Func<TState, float[], int, float[], int, TState> update,
            int outputChannels = 1, int oversample = 1)
        {
            if (reset)
            {
                state = create(sampleClock);
                if (oversample != 1)
                {
                    decimator = new Decimator(oversample, oversample);
                }
            }

            if (buffer.WaveFormat.Channels != outputChannels)
            {
                buffer = new AudioSampleBuffer(
                    WaveFormat.CreateIeeeFloatWaveFormat(WaveOutput.InternalFormat.SampleRate, outputChannels));
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

                        if (decimator == null)
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
                            var increment = 1.0 / WaveOutput.InternalFormat.SampleRate / decimator.Oversample;
                            var inputChannels = input?.WaveFormat?.Channels ?? 0;
                            for (int i = 0; i < count / outputChannels; i++)
                            {
                                for (int j = 0; j < decimator.Oversample; j++)
                                {
                                    state = update(state, inputBuffer, i * inputChannels, decimator.Input, j);
                                    sampleClock.IncrementTime(increment);
                                }

                                b[i * outputChannels] = decimator.Process();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AudioEngine.Log(e.Message);
                    }
                };

                updateFunction = update;
            }

            return buffer;
        }
    }
}