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
    public class AudioLoopRegion<TState> : IAudioNode<IAudioLoopRegionConfig> where TState : class
    {
        private readonly ILogger _logger;
        private readonly AudioSampleFrameClock _clock = new AudioSampleFrameClock();
        private Func<TState, AudioChannels, TState> _updateFunc;
        private TState _state;
        private TransformBlock<AudioDataMessage, AudioDataMessage> _processor;

        private AudioNodeSupport<IAudioLoopRegionConfig> _support;
        public AudioParams AudioParams => _support.AudioParams;
        public IAudioLoopRegionConfig Config => _support.Config;
        public IAudioLoopRegionConfig LastConfig => _support.LastConfig;
        public AudioLink Output => _support.Output;
        public AudioLoopRegion()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioLoopRegion<TState>>();
            _support = new AudioNodeSupport<IAudioLoopRegionConfig>(this);

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
                    _support.HandleError(e);
                    return input;
                }
            });

            Output.SourceBlock = _processor;
        }

        public bool IsInputValid(IAudioLoopRegionConfig next)
        {
            return true;
        }

        public void OnAnyChange()
        {
        }

        public void Update(
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
            Config.OutputChannels = outputChannels;
            Config.Reset = reset;
            Config.Input = input;

            _support.Update();
        }

        public void OnConnect(AudioLink input)
        {
            var inputChannels = input.Format.Channels;
            if (Config.OutputChannels == 0)
            {
                Config.OutputChannels = inputChannels;
            }

            Output.Format = input.Format.WithChannels(Config.OutputChannels);

            _logger.Information("Creating Buffer & Processor for Loop {@InputFormat} => {@OutputFormat}",
                input.Format, Output.Format);

            _support.AddLink(input.SourceBlock.LinkTo(_processor));
        }

        public void OnDisconnect(AudioLink link)
        {
            _support.DisposeLinks();
            _logger.Information("Disconnected from loop region");
        }

        public void OnStart()
        {
        }

        public void OnStop()
        {
        }

        public void Dispose()
        {
            _support.Dispose();
        }
    }
}