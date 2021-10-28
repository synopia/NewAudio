using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Serilog;
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
        private readonly ILogger _logger = Log.Logger;
        private readonly AudioSampleFrameClock _clock = new AudioSampleFrameClock();
        private Func<TState, AudioSampleAccessor, TState> _updateFunc;
        private IDisposable _link;
        private TState _state;
        private bool _bypass;

        public AudioLink Update(
            Func<IFrameClock, TState> create,
            Func<TState, AudioSampleAccessor, TState> update,
            bool reset,
            bool bypass,
            AudioLink input, out bool inProgress, int outputChannels = 0
        )
        {
            if (_state == null && create!=null)
            {
                _state = create(_clock);
            }

            _bypass = bypass;
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

                    _logger.Information("Creating Buffer & Processor for Loop {@InputFormat} => {@OutputFormat}", input.Format, outputFormat);
                    var samples = input.Format.SampleCount;
                    var inputBufferSize = input.Format.BufferSize;
                    var outputBufferSize = outputFormat.BufferSize;

                    var transformer = new TransformBlock<AudioBuffer, AudioBuffer>(inp =>
                    {
                        if (_bypass)
                        {
                            Array.Clear(inp.Data, 0, inp.Count);
                            return inp;
                        }
                        var sampleAccessor = new AudioSampleAccessor();

                        try
                        {
                            if (inp.Count != inputBufferSize)
                            {
                                throw new Exception($"Expected Input size: {inputBufferSize}, actual: {inp.Count}");
                            }

                            var output = AudioCore.Instance.BufferFactory.GetBuffer(outputBufferSize);
                            output.Time = inp.Time;

                            _clock.Init(inp.Time.DTime);

                            if (_state != null && _updateFunc != null)
                            {
                                sampleAccessor.Update(output.Data, inp.Data, outputChannels, inputChannels);
                                var increment = 1.0d / input.Format.SampleRate;
                                for (int i = 0; i < samples; i++)
                                {
                                    sampleAccessor.UpdateLoop(i, i);
                                    _state = _updateFunc(_state, sampleAccessor);
                                    _clock.IncrementTime(increment);
                                }
                            }

                            return output;
                        }
                        catch (Exception e)
                        {
                            _logger.Error("{e}" , e);
                            throw;
                        }
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