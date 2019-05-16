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

    public class AudioSampleLoop<TState> : BaseAudioNode where TState : class
    {
        private bool reset;
        private AudioSampleBuffer input;
        private Func<IFrameClock, TState> createFunc;
        private Func<TState, AudioSampleAccessor, TState> updateFunc;
        private int outputChannels;
        private int oversampling;
        private bool hasChanges;

        private TState state;
        private readonly AudioSampleFrameClock sampleClock = new AudioSampleFrameClock();
        private AudioSampleBuffer output = new AudioSampleBuffer(WaveOutput.SingleChannelFormat);
        private AudioSampleLoopProcessor processor;

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

            hasChanges = reset != this.reset
                         || input != this.input
                         || outputChannels != this.outputChannels
                         || oversample != oversampling;

            this.reset = reset;
            this.input = input;
            createFunc = create;
            updateFunc = update;
            this.outputChannels = outputChannels;
            oversampling = oversample;

            if (hasChanges || reset || HotSwapped)
            {
                AudioEngine.Log(
                    $"AudioSampleLoop({state}) configuration changed outChannels={outputChannels}, oversampling={oversample}");

                if (IsValid())
                {
                    processor = new AudioSampleLoopProcessor(state, input, sampleClock, update, outputChannels,
                        oversample);
                    output = processor.Build();
                }
                else
                {
                    output?.Dispose();
                    output = null;
                }

                HotSwapped = false;
            }

            processor.updateFunc = update;

            return output;
        }


        private bool IsValid()
        {
            return createFunc != null && updateFunc != null;
        }

        private class AudioSampleLoopProcessor : IAudioProcessor
        {
            public TState state;
            private readonly AudioSampleBuffer input;
            public Func<TState, AudioSampleAccessor, TState> updateFunc;
            private readonly int outputChannels;
            private int InputChannels => input?.WaveFormat?.Channels ?? 0;
            private readonly int oversampling;
            private float[] inputBuffer;
            private readonly AudioSampleAccessor accessor = new AudioSampleAccessor();
            private Decimator[] decimators;
            private float[] oversampleBuffer;
            private float[] oversampleBuffer2;
            private readonly AudioSampleFrameClock sampleClock;

            public AudioSampleLoopProcessor(TState state, AudioSampleBuffer input, AudioSampleFrameClock sampleClock,
                Func<TState, AudioSampleAccessor, TState> updateFunc, int outputChannels, int oversampling)
            {
                this.state = state;
                this.input = input;
                this.updateFunc = updateFunc;
                this.outputChannels = outputChannels;
                this.oversampling = oversampling;
                this.sampleClock = sampleClock;
                if (oversampling > 1)
                {
                    BuildOversampling();
                }
            }

            public AudioSampleBuffer Build()
            {
                return new AudioSampleBuffer(
                    WaveFormat.CreateIeeeFloatWaveFormat(WaveOutput.InternalFormat.SampleRate, outputChannels))
                {
                    Processor = this
                };
            }

            public int Read(float[] buffer, int offset, int count)
            {
                if (input != null)
                {
                    var inputSamples = count * InputChannels / outputChannels;
                    if (inputBuffer == null || inputBuffer.Length != inputSamples)
                    {
                        inputBuffer = new float[inputSamples];
                    }

                    input.Read(inputBuffer, offset, inputSamples);
                }
                else
                {
                    inputBuffer = null;
                }


                if (oversampling <= 1)
                {
                    LoopNormal(buffer, count);
                }
                else
                {
                    LoopOversampling(buffer, count);
                }

                return count;
            }

            private void LoopOversampling(float[] b, int count)
            {
                var increment = 1.0 / WaveOutput.InternalFormat.SampleRate / oversampling;
                for (int i = 0; i < count / outputChannels; i++)
                {
                    accessor.Update(oversampleBuffer, inputBuffer, outputChannels, InputChannels);
                    for (int j = 0; j < oversampling; j++)
                    {
                        accessor.UpdateLoop(i, j);
                        state = updateFunc(state, accessor);
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
                            for (int k = 0; k < oversampling; k++)
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
                accessor.Update(b, inputBuffer, outputChannels, InputChannels);
                var increment = 1.0 / WaveOutput.InternalFormat.SampleRate;
                for (int i = 0; i < count / outputChannels; i++)
                {
                    accessor.UpdateLoop(i, i);
                    state = updateFunc(state, accessor);
                    sampleClock.IncrementTime(increment);
                }
            }

            private void BuildOversampling()
            {
                AudioEngine.Log($"AudioSampleLoop: {state} oversampling enabled {oversampling}");
                decimators = new Decimator[outputChannels];
                oversampleBuffer = new float[oversampling * outputChannels];
                oversampleBuffer2 = new float[oversampling];
                for (int i = 0; i < outputChannels; i++)
                {
                    decimators[i] = new Decimator(oversampling, oversampling);
                }
            }
        }
    }
}