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
        private Func<TState, AudioSampleAccessor, TState> updateFunction;
        private float[] inputBuffer;
        private int oversample;
        private Decimator[] decimators;
        private float[] oversampleBuffer;
        private float[] oversampleBuffer2;
        private int inputChannels;
        private AudioSampleBuffer input;

        private AudioSampleAccessor accessor = new AudioSampleAccessor();

        public AudioSampleBuffer Update(
            bool reset,
            AudioSampleBuffer input,
            Func<IFrameClock, TState> create, Func<TState, AudioSampleAccessor, TState> update,
            int outputChannels = 1, int oversample = 1)
        {
            if (reset || state == null)
            {
                state = create(sampleClock);
                AudioEngine.Log($"AudioSampleLoop: Created {state}");
            }

            if (this.input != input || (this.input?.WaveFormat?.Channels != input?.WaveFormat.Channels))
            {
                AudioEngine.Log(
                    $"AudioSampleLoop: {state} input changed {inputChannels} to {input?.WaveFormat?.Channels ?? 0}");
                this.input = input;
                inputChannels = input?.WaveFormat?.Channels ?? 0;
                reset = true;
            }

            if (buffer.WaveFormat.Channels != outputChannels)
            {
                AudioEngine.Log($"AudioSampleLoop: {state} outputChannels changed {outputChannels}");
                if (buffer != null)
                {
                    buffer.Update = (floats, i, arg3) => { };
                }

                buffer = new AudioSampleBuffer(
                    WaveFormat.CreateIeeeFloatWaveFormat(WaveOutput.InternalFormat.SampleRate, outputChannels));
                this.oversample = 0;
            }

            if (oversample != this.oversample)
            {
                this.oversample = oversample;
                if (oversample > 1)
                {
                    AudioEngine.Log($"AudioSampleLoop: {state} oversampling enabled {oversample}");
                    decimators = new Decimator[outputChannels];
                    oversampleBuffer = new float[oversample * outputChannels];
                    oversampleBuffer2 = new float[oversample];
                    for (int i = 0; i < outputChannels; i++)
                    {
                        decimators[i] = new Decimator(oversample, oversample);
                    }
                }
                else
                {
                    AudioEngine.Log($"AudioSampleLoop: {state} oversampling disabled");
                }
            }

            if (update != updateFunction || reset)
            {
                buffer.Update = (b, offset, count) =>
                {
                    try
                    {
                        if (this.input != null)
                        {
                            var inputSamples = count * inputChannels / outputChannels;

                            if (inputBuffer == null || inputBuffer.Length != inputSamples)
                            {
                                inputBuffer = new float[inputSamples];
                            }

                            this.input.Read(inputBuffer, offset, inputSamples);
                        }

                        accessor.Update(b, inputBuffer, outputChannels, inputChannels);

                        if (oversample == 1)
                        {
                            var increment = 1.0 / WaveOutput.InternalFormat.SampleRate;
                            for (int i = 0; i < count / outputChannels; i++)
                            {
                                accessor.UpdateLoop(i, i);
//                                state = update(state, inputBuffer, i * inputChannels, b, i * outputChannels);
                                state = update(state, accessor);
                                sampleClock.IncrementTime(increment);
                            }
                        }
                        else
                        {
                            var increment = 1.0 / WaveOutput.InternalFormat.SampleRate / oversample;
                            for (int i = 0; i < count / outputChannels; i++)
                            {
                                accessor.Update(b, oversampleBuffer, outputChannels, inputChannels);
                                for (int j = 0; j < oversample; j++)
                                {
                                    accessor.UpdateLoop(i, j);
//                                    state = update(state, inputBuffer, i * inputChannels, oversampleBuffer,
//                                        j * outputChannels);
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