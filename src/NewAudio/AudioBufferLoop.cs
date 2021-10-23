using System;
using System.Threading.Tasks.Dataflow;
using VL.Lib.Animation;
using VL.NewAudio;

namespace NewAudio
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

        public IObservable<FrameFinishedMessage> GetFrameFinished()
        {
            throw new NotImplementedException();
        }
    }

    public class AudioBufferLoop<TState> : AudioNodeTransformer where TState : class
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioBufferLoop");
        public readonly AudioSampleFrameClock SampleClock;
        private TState _state;
        private Func<IFrameClock, TState> _createFunc;
        private Func<TState, AudioSampleAccessor, TState> _updateFunc;
        private readonly AudioSampleAccessor _sampleAccessor = new AudioSampleAccessor();
        private IDisposable _link;

        public AudioBufferLoop()
        {
            SampleClock = new AudioSampleFrameClock();
        }

        public AudioLink Update(
            Func<IFrameClock, TState> create,
            Func<TState, AudioSampleAccessor, TState> update,
            bool reset,
            bool abort,
            AudioLink input, out bool inProgress, int outputChannels = 0,
            int oversample = 1
        )
        {
            _createFunc = create;
            _updateFunc = update;
            if (reset)
            {
                Stop(true);
            }

            if (abort)
            {
                Stop(false);
            }

            if (_link == null && input != null)
            {
                var inputChannels = input?.WaveFormat.Channels ?? 0;
                if (outputChannels == 0)
                {
                    outputChannels = inputChannels;
                }

                _logger.Info($"Creating Buffer & Processor for Loop {input?.Format}");
                var samples = input.Format.BufferSize / inputChannels;
                var inputSamples = input.Format.BufferSize;
                var outputSamples = samples * outputChannels;

                var transformer = new TransformBlock<AudioBuffer, AudioBuffer>(inp =>
                {
                    float[] output = new float[outputSamples];
                    TState state = _state;
                    if (state == null)
                    {
                        state = _createFunc?.Invoke(SampleClock);
                    }

                    if (state != null && _updateFunc != null)
                    {
                        // input?.Read(output, 0, inputSamples);
                        _sampleAccessor.Update(output, inp.Data, outputChannels, inputChannels);
                        var increment = 1.0 / input.Format.SampleRate;
                        for (int i = 0; i < samples; i++)
                        {
                            _sampleAccessor.UpdateLoop(i, i);
                            state = _updateFunc(state, _sampleAccessor);
                            SampleClock.IncrementTime(increment);
                        }
                    }

                    _state = state;
                    if (_state is IDisposable fState)
                    {
                        fState.Dispose();
                    }

                    _state = default(TState);

                    return new AudioBuffer(null, output, outputSamples);
                }, new ExecutionDataflowBlockOptions()
                {
                });
                _link = input.SourceBlock.LinkTo(transformer);
                Output.SourceBlock = transformer;
                Output.Format = input.Format.WithChannels(outputChannels);
            }

            inProgress = _link != null;

            return Output;
        }

        private void Stop(bool shouldRun)
        {
            _link?.Dispose();
        }

        public override void Dispose()
        {
            Stop(false);
        }
    }
}