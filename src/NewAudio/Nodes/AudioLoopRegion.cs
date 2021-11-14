using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using VL.Lib.Animation;

namespace NewAudio.Nodes
{
    public class AudioLoopRegionParams : AudioParams
    {
        public AudioParam<bool> Bypass;
        public AudioParam<int> OutputChannels;
    }

    public class AudioLoopRegion<TState> : AudioNode
        where TState : class
    {
        public override string NodeName => "Loop";
        private TransformBlock<AudioDataMessage, AudioDataMessage> _processor;
        private readonly AudioSampleFrameClock _clock = new();

        private Func<TState, AudioChannels, TState> _updateFunc;
        private TState _state;
        public AudioLoopRegionParams Params { get; }

        public AudioLoopRegion()
        {
            InitLogger<AudioLoopRegion<TState>>();
            Params = AudioParams.Create<AudioLoopRegionParams>();
            Logger.Information("Audio loop region created");
        }

        public AudioLink Update(
            AudioLink input,
            Func<IFrameClock, TState> create,
            Func<TState, AudioChannels, TState> update,
            bool reset,
            bool bypass,
            int outputChannels = 0,
            int bufferSize = 4
        )
        {
            if (_state == null && create != null)
            {
                _state = create(_clock);
            }

            _updateFunc = update;
            Params.Bypass.Value = bypass;
            Params.OutputChannels.Value = outputChannels > 0 ? outputChannels : input?.Format.Channels ?? 0;
            PlayParams.Update(input, Params.HasChanged, bufferSize);
            
            return Update();
        }


        public override bool Play()
        {
            if (PlayParams.Input.Value != null &&
                Params.OutputChannels.Value > 0 &&
                PlayParams.InputFormat.Value.Channels > 0 &&
                PlayParams.InputFormat.Value.SampleCount > 0)
            {
                Params.Commit();
                var input = PlayParams.Input.Value;
                var inputChannels = input.Format.Channels;
                if (Params.OutputChannels.Value == 0)
                {
                    Params.OutputChannels.Value = inputChannels;
                }

                Output.Format = input.Format.WithChannels(Params.OutputChannels.Value);
                Logger.Information("Creating Buffer & Processor for Loop {@InputFormat} => {@OutputFormat}",
                    input.Format, Output.Format);

                InitProcessor();
                Output.SourceBlock = _processor;
                TargetBlock = _processor;
                return true;
            }

            return false;
        }

        public override void Stop()
        {
            _processor?.Complete();
            _processor?.Completion.ContinueWith((t) =>
            {
                _processor = null;
                Logger.Information("Transform block stopped, status={Status}", t.Status);
                return true;
            });
            
            TargetBlock = null;
            Output.SourceBlock = null;
        }


        private void InitProcessor()
        {
            if (_processor != null)
            {
                Logger.Warning("TransformBlock != null!");
            }

            _processor = new TransformBlock<AudioDataMessage, AudioDataMessage>(input =>
            {
                if (Params.Bypass.Value)
                {
                    Array.Clear(input.Data, 0, input.BufferSize);
                    return input;
                }

                var output = new AudioDataMessage(Output.Format, Output.Format.SampleCount)
                {
                    Time = input.Time
                };
                try
                {
                    var channels = new AudioChannels();

                    var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;
                    if (input.BufferSize != inputBufferSize)
                    {
                        throw new Exception($"Expected Input size: {inputBufferSize}, actual: {input.BufferSize}");
                    }

                    var samples = PlayParams.Input.Value.Format.SampleCount;
                    var outputChannels = Output.Format.Channels;
                    var inputChannels = PlayParams.Input.Value.Format.Channels;

                    _clock.Init(input.Time.DTime);

                    if (_state != null && _updateFunc != null)
                    {
                        channels.Update(output.Data, input.Data, outputChannels, inputChannels);
                        var increment = 1.0d / PlayParams.Input.Value.Format.SampleRate;
                        for (var i = 0; i < samples; i++)
                        {
                            channels.UpdateLoop(i, i);
                            _state = _updateFunc(_state, channels);
                            _clock.IncrementTime(increment);
                        }
                    }
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "TransformBlock");
                }

                return output;
            }, new ExecutionDataflowBlockOptions()
            {
                BoundedCapacity = 1,
                MaxDegreeOfParallelism = 1
            });
        }

        public override string DebugInfo()
        {
            return $"[{this}, in/out={_processor?.InputCount}/{_processor?.OutputCount}, {base.DebugInfo()}]";
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _processor?.Complete();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}