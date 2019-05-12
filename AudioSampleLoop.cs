using System;
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
        private AudioSampleBuffer buffer = new AudioSampleBuffer(WaveOutput.InternalFormat);
        private Func<TState, float[], Tuple<TState, float[]>> updateFunction;
        private float[] inputBuffer;

        private float[] tempInput;

        public AudioSampleBuffer Update(
            AudioSampleBuffer input,
            bool reset,
            Func<IFrameClock, TState> create, Func<TState, float[], Tuple<TState, float[]>> update)
        {
            if (reset)
            {
                state = create(sampleClock);
            }

            if (update != updateFunction || !input.WaveFormat.Equals(buffer.WaveFormat))
            {
                if (input != null && !input.WaveFormat.Equals(buffer.WaveFormat))
                {
                    buffer = new AudioSampleBuffer(input.WaveFormat);
                }

                buffer.Update = (b, offset, count) =>
                {
                    if (input != null)
                    {
                        if (inputBuffer == null || inputBuffer.Length < offset + count)
                        {
                            inputBuffer = new float[count];
                        }

                        input.Read(inputBuffer, offset, count);
                    }

                    var channels = buffer.WaveFormat.Channels;
                    if (tempInput == null || tempInput.Length != channels)
                    {
                        tempInput = new float[channels];
                    }

                    var increment = 1.0 / buffer.WaveFormat.SampleRate;
                    for (int i = 0; i < count / channels; i++)
                    {
                        for (int j = 0; j < channels; j++)
                        {
                            tempInput[j] = inputBuffer?[i * channels + j + offset] ?? 0;
                        }

                        var result = update(state, tempInput);
                        var outp = result.Item2;
                        for (int j = 0; j < channels; j++)
                        {
                            b[i * channels + j + offset] = outp[j];
                        }

                        state = result.Item1;

                        sampleClock.IncrementTime(increment);
                    }
                };
                updateFunction = update;
            }

            return buffer;
        }
    }
}