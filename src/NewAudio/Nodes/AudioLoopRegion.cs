using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using VL.Lib.Animation;

namespace NewAudio.Nodes
{
    public interface IAudioLoopRegionConfig : IAudioNodeConfig
    {
        bool Bypass { get; set; }
        int OutputChannels { get; set; }
    }
    public class AudioLoopRegion<TState> : AudioNode<IAudioLoopRegionConfig> where TState : class
    {
        private readonly ILogger _logger;
        private readonly AudioSampleFrameClock _clock = new AudioSampleFrameClock();
        private Func<TState, AudioChannels, TState> _updateFunc;
        private TState _state;
        private TransformBlock<AudioDataMessage, AudioDataMessage> _processor;

        public AudioLoopRegion()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioLoopRegion<TState>>();

            _processor = new TransformBlock<AudioDataMessage, AudioDataMessage>(input =>
            {
                if (Config.Bypass)
                {
                    Array.Clear(input.Data, 0, input.BufferSize);
                    return input;
                }

                try
                {
                    var channels = new AudioChannels();

                    var inputBufferSize = Config.Input.Format.BufferSize;
                    if (input.BufferSize != inputBufferSize)
                    {
                        throw new Exception($"Expected Input size: {inputBufferSize}, actual: {input.BufferSize}");
                    }

                    var samples = Config.Input.Format.SampleCount;
                    var outputBufferSize = Output.Format.BufferSize;
                    var outputChannels = Output.Format.Channels;
                    var inputChannels = Config.Input.Format.Channels;
                    var output = new AudioDataMessage(Output.Format, Output.Format.SampleCount)
                    {
                        Time = input.Time
                    };

                    _clock.Init(input.Time.DTime);

                    if (_state != null && _updateFunc != null)
                    {
                        channels.Update(output.Data, input.Data, outputChannels, inputChannels);
                        var increment = 1.0d / Config.Input.Format.SampleRate;
                        for (var i = 0; i < samples; i++)
                        {
                            channels.UpdateLoop(i, i);
                            _state = _updateFunc(_state, channels);
                            _clock.IncrementTime(increment);
                        }
                    }

                    return output;
                }
                catch (Exception e)
                {
                    _logger.Error("{e}", e);
                    HandleError(e);
                    return input;
                }
            });

            Output.SourceBlock = _processor;
        }

        public AudioLink Update(
            AudioLink input,
            Func<IFrameClock, TState> create,
            Func<TState, AudioChannels, TState> update,
            bool reset,
            bool bypass,
            int outputChannels = 0
        )
        {
            if (_state == null && create != null)
            {
                _state = create(_clock);
            }
            _updateFunc = update;

            Config.Bypass = bypass;
            Config.OutputChannels = outputChannels > 0 ? outputChannels : input?.Format.Channels ?? 0;
            Config.Reset = reset;
            Config.Input = input;

            return Update();
        }

        protected override bool IsInputValid(IAudioLoopRegionConfig next)
        {
            return next.OutputChannels>0 && next.Input?.Format.Channels>0 && next.Input?.Format.SampleCount>0;
        }

        public override string DebugInfo()
        {
            return $"LOOP [{_processor.InputCount}/{_processor.OutputCount}, {_processor.Completion.Status}]";
        }

        protected override void OnConnect(AudioLink input)
        {
            var inputChannels = input.Format.Channels;
            if (Config.OutputChannels == 0)
            {
                Config.OutputChannels = inputChannels;
            }

            Output.Format = input.Format.WithChannels(Config.OutputChannels);

            _logger.Information("Creating Buffer & Processor for Loop {@InputFormat} => {@OutputFormat}",
                input.Format, Output.Format);

            AddLink(input.SourceBlock.LinkTo(_processor));
        }
    }
}