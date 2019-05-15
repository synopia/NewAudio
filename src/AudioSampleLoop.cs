using System;
using NAudio.Wave;
using VL.Lib.Animation;

namespace VL.NewAudio
{
    public class AudioSampleFrameClock : IFrameClock
    {
        private Time frameTime;

        public Time Time => frameTime;
        public double TimeDifference { get; private set; }

        public void IncrementTime(double diff)
        {
            frameTime += diff;
            TimeDifference = diff;
        }

        public IObservable<FrameTimeMessage> GetTicks()
        {
            throw new NotImplementedException();
        }
    }

    public class AudioSampleLoop<TState> where TState : class
    {
        private struct Configuration
        {
            public bool Reset;
            public AudioSampleBuffer Input;
            public Func<IFrameClock, TState> CreateFunc;
            public Func<TState, AudioSampleAccessor, TState> UpdateFunc;
            public int OutputChannels;
            public int InputChannels => Input?.WaveFormat?.Channels ?? 0;
            public int Oversampling;
            public bool HasChanges;

            public void Update(
                bool reset,
                AudioSampleBuffer input,
                Func<IFrameClock, TState> create,
                Func<TState, AudioSampleAccessor, TState> update,
                int outputChannels,
                int oversampling)
            {
                HasChanges = reset != Reset
                             || input?.GetHashCode() != Input?.GetHashCode()
                             || outputChannels != OutputChannels
                             || oversampling != Oversampling;

                Reset = reset;
                Input = input;
                CreateFunc = create;
                UpdateFunc = update;
                OutputChannels = outputChannels;
                Oversampling = oversampling;
            }

            public bool IsValid()
            {
                return CreateFunc != null && UpdateFunc != null && Input != null;
            }
        }

        private TState state;
        private readonly AudioSampleFrameClock sampleClock = new AudioSampleFrameClock();
        private AudioSampleBuffer buffer = new AudioSampleBuffer(WaveOutput.SingleChannelFormat);
        private float[] inputBuffer;
        private Decimator[] decimators;
        private float[] oversampleBuffer;
        private float[] oversampleBuffer2;
        private Configuration config = new Configuration();
        private AudioSampleAccessor accessor = new AudioSampleAccessor();

        public AudioSampleBuffer Update(
            bool reset,
            AudioSampleBuffer input,
            Func<IFrameClock, TState> create,
            Func<TState, AudioSampleAccessor, TState> update,
            int outputChannels = 1,
            int oversample = 1)
        {
            if (reset || state == null)
            {
                state = create(sampleClock);
                AudioEngine.Log($"AudioSampleLoop: Created {state}");
            }

            config.Update(reset, input, create, update, outputChannels, oversample);

            if (config.HasChanges)
            {
                AudioEngine.Log(
                    $"AudioSampleLoop configuration changed outChannels={outputChannels}, oversampling={oversample}");

                if (config.IsValid())
                {
                    if (config.Oversampling > 1)
                    {
                        BuildOversampling();
                    }

                    Build();
                }
                else
                {
                    buffer?.Dispose();
                    buffer = null;
                }
            }

            return buffer;
        }

        private void Build()
        {
            var outputChannels = config.OutputChannels;
            var inputChannels = config.InputChannels;
            buffer = new AudioSampleBuffer(
                WaveFormat.CreateIeeeFloatWaveFormat(WaveOutput.InternalFormat.SampleRate, outputChannels));
            buffer.Update = (b, offset, count) =>
            {
                try
                {
                    if (config.Input != null)
                    {
                        var inputSamples = count * inputChannels / outputChannels;
                        if (inputBuffer == null || inputBuffer.Length != inputSamples)
                        {
                            inputBuffer = new float[inputSamples];
                        }

                        config.Input.Read(inputBuffer, offset, inputSamples);
                    }
                    else
                    {
                        inputBuffer = null;
                    }


                    if (config.Oversampling <= 1)
                    {
                        LoopNormal(b, count);
                    }
                    else
                    {
                        LoopOversampling(b, count);
                    }
                }
                catch (Exception e)
                {
                    AudioEngine.Log(e.Message);
                    AudioEngine.Log(e.StackTrace);
                }

                return count;
            };
        }

        private void LoopOversampling(float[] b, int count)
        {
            var outputChannels = config.OutputChannels;

            var increment = 1.0 / WaveOutput.InternalFormat.SampleRate / config.Oversampling;
            for (int i = 0; i < count / outputChannels; i++)
            {
                accessor.Update(oversampleBuffer, inputBuffer, outputChannels, config.InputChannels);
                for (int j = 0; j < config.Oversampling; j++)
                {
                    accessor.UpdateLoop(i, j);
                    state = config.UpdateFunc(state, accessor);
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
                        for (int k = 0; k < config.Oversampling; k++)
                        {
                            oversampleBuffer2[k] = oversampleBuffer[k * outputChannels + j];
                        }

                        b[i * outputChannels + j] = decimators[j].Process(oversampleBuffer2);
                    }
                }
            }
        }

        private void LoopNormal(float[] b, int count)
        {
            accessor.Update(b, inputBuffer, config.OutputChannels, config.InputChannels);
            var increment = 1.0 / WaveOutput.InternalFormat.SampleRate;
            for (int i = 0; i < count / config.OutputChannels; i++)
            {
                accessor.UpdateLoop(i, i);
                state = config.UpdateFunc(state, accessor);
                sampleClock.IncrementTime(increment);
            }
        }

        private void BuildOversampling()
        {
            AudioEngine.Log($"AudioSampleLoop: {state} oversampling enabled {config.Oversampling}");
            decimators = new Decimator[config.OutputChannels];
            oversampleBuffer = new float[config.Oversampling * config.OutputChannels];
            oversampleBuffer2 = new float[config.Oversampling];
            for (int i = 0; i < config.OutputChannels; i++)
            {
                decimators[i] = new Decimator(config.Oversampling, config.Oversampling);
            }
        }
    }
}