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
                var inputChannels = input.WaveFormat.Channels;
                if (outputChannels == 0)
                {
                    outputChannels = inputChannels;
                }
                var outputFormat = input.Format.WithChannels(outputChannels);
                
                _logger.Info($"Creating Buffer & Processor for Loop {input.Format} => {outputFormat}");
                var samples = input.Format.SampleCount;
                var inputSamples = input.Format.BufferSize;
                var outputSamples = outputFormat.BufferSize;

                var transformer = new TransformBlock<AudioBuffer, AudioBuffer>(inp =>
                {
                    if (inp.Count != inputSamples)
                    {
                        _logger.Error($"Expected Input size: {inputSamples}, actual: {inp.Count}");
                        return inp;
                    }
                    var output = AudioCore.Instance.BufferFactory.GetBuffer(outputSamples);
                    TState state = _state;
                    if (state == null)
                    {
                        state = _createFunc?.Invoke(SampleClock);
                    }

                    if (state != null && _updateFunc != null)
                    {
                        _sampleAccessor.Update(output.Data, inp.Data, outputChannels, inputChannels);
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

                    return output;
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
            _link = null;
        }

        public override void Dispose()
        {
            Stop(false);
        }
    }
}