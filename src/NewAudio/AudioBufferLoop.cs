using System;
using System.Threading;
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

        public void Init(double time)
        {
            frameTime = time;
        }

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
        private Func<IFrameClock, TState> _createFunc;
        private Func<TState, AudioSampleAccessor, TState> _updateFunc;
        private IDisposable _link;


        public AudioBufferLoop()
        {
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

            if (reset || input != Input)
            {
                _link?.Dispose();
                Connect(input);

                if (input != null)
                {
                    var inputChannels = input.WaveFormat.Channels;
                    if (outputChannels == 0)
                    {
                        outputChannels = inputChannels;
                    }

                    var outputFormat = input.Format.WithChannels(outputChannels);

                    _logger.Info($"Creating Buffer & Processor for Loop {input.Format} => {outputFormat}");
                    var samples = input.Format.SampleCount;
                    var inputBufferSize = input.Format.BufferSize;
                    var outputBufferSize = outputFormat.BufferSize;

                    var transformer = new TransformBlock<AudioBuffer, AudioBuffer>(inp =>
                    {
                        var sampleClock = new AudioSampleFrameClock();
                        var sampleAccessor = new AudioSampleAccessor();

                        try
                        {
                            _logger.Trace(
                                $"ID={Thread.CurrentThread.GetHashCode()} Received Data {inp.Count} Time={inp.Time}");
                            if (inp.Count != inputBufferSize)
                            {
                                throw new Exception($"Expected Input size: {inputBufferSize}, actual: {inp.Count}");
                            }

                            var output = AudioCore.Instance.BufferFactory.GetBuffer(outputBufferSize);
                            output.Time = inp.Time;
                            output.DTime = inp.Time;
                            sampleClock.Init(inp.DTime);

                            var state = _createFunc?.Invoke(sampleClock);

                            if (state != null && _updateFunc != null)
                            {
                                sampleAccessor.Update(output.Data, inp.Data, outputChannels, inputChannels);
                                var increment = 1.0d / input.Format.SampleRate;
                                for (int i = 0; i < samples; i++)
                                {
                                    sampleAccessor.UpdateLoop(i, i);
                                    state = _updateFunc(state, sampleAccessor);
                                    sampleClock.IncrementTime(increment);
                                }
                            }

                            return output;
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e);
                            throw;
                        }
                    }, new ExecutionDataflowBlockOptions()
                    {
                        // BoundedCapacity = 1,
                        // SingleProducerConstrained = true,
                        // MaxDegreeOfParallelism = 1,
                        // MaxMessagesPerTask = 1
                    });
                    _link = input.SourceBlock.LinkTo(transformer);
                    Output.SourceBlock = transformer;
                    Output.Format = input.Format.WithChannels(outputChannels);
                }
            }

            inProgress = _link != null;

            return Output;
        }

        public override void Dispose()
        {
            _link?.Dispose();
            base.Dispose();
        }
    }
}