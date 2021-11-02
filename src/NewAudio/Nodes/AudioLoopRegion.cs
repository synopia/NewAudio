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
    public class AudioLoopRegion<TState> : BaseNode where TState : class
    {
        private readonly ILogger _logger;
        private readonly AudioSampleFrameClock _clock = new AudioSampleFrameClock();
        private Func<TState, AudioChannels, TState> _updateFunc;
        private TState _state;
        private bool _bypass;

        private int _outputChannels;
        
        public AudioLoopRegion()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioLoopRegion<TState>>();
           
            var transformer = new TransformBlock<AudioDataMessage, AudioDataMessage>(input =>
            {
                if (_bypass)
                {
                    Array.Clear(input.Data, 0, input.BufferSize);
                    return input;
                }

                try
                {
                    var channels = new AudioChannels();

                    var inputBufferSize = Input.Format.BufferSize;
                    if (input.BufferSize != inputBufferSize)
                    {
                        throw new Exception($"Expected Input size: {inputBufferSize}, actual: {input.BufferSize}");
                    }

                    var samples = Input.Format.SampleCount;
                    var outputBufferSize = Output.Format.BufferSize;
                    var outputChannels = Output.Format.Channels;
                    var inputChannels = Input.Format.Channels;
                    var output = new AudioDataMessage(Output.Format, Output.Format.BufferSize)
                    {
                        Time = input.Time
                    };

                    _clock.Init(input.Time.DTime);

                    if (_state != null && _updateFunc != null)
                    {
                        channels.Update(output.Data, input.Data, outputChannels, inputChannels);
                        var increment = 1.0d / Input.Format.SampleRate;
                        for (int i = 0; i < samples; i++)
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
                    _logger.Error("{e}" , e);
                    HandleError(e);
                    return input;
                }
            });
            
            OnConnect += link =>
            {
                var inputChannels = Input.Format.Channels;
                if (_outputChannels == 0)
                {
                    _outputChannels = inputChannels;
                }

                Output.Format = Input.Format.WithChannels(_outputChannels);

                _logger.Information("Creating Buffer & Processor for Loop {@InputFormat} => {@OutputFormat}", Input.Format, Output.Format);

                AddLink(link.SourceBlock.LinkTo(transformer));
            };
            OnDisconnect += link =>
            {
                DisposeLinks();
                _logger.Information("Disconnected from loop region");
            };
            

            Output.SourceBlock = transformer;
            
        }

        public AudioLink Update(
            AudioLink input,
            Func<IFrameClock, TState> create,
            Func<TState, AudioChannels, TState> update,
            bool reset,
            bool bypass,
            out bool inProgress, int outputChannels = 0
        )
        {
            if (_state == null && create!=null)
            {
                _state = create(_clock);
            }

            _bypass = bypass;
            _updateFunc = update;
      
            UpdateInput(input, reset);

            inProgress = Input != null;

            return Output;
        }

        protected override void Start()
        {
        }

        protected override void Stop()
        {
        }

        public override void Dispose()
        {
            
            base.Dispose();
        }
    }
}