﻿using System;
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
    public class AudioLoopRegionConfig : AudioNodeConfig
    {
        public AudioParam<bool> Bypass;
        public AudioParam<int> OutputChannels;
    }
    public class AudioLoopRegion<TState> : AudioNode<AudioLoopRegionConfig> where TState : class
    {
        private readonly ILogger _logger;
        private TransformBlock<AudioDataMessage, AudioDataMessage> _processor;
        private IDisposable _inputBufferLink;

        private readonly AudioSampleFrameClock _clock = new AudioSampleFrameClock();
        
        private Func<TState, AudioChannels, TState> _updateFunc;
        private TState _state;

        public AudioLoopRegion()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioLoopRegion<TState>>();
            _logger.Information("Audio loop region created");
        }

        public  AudioLink Update(
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

            Config.Bypass.Value = bypass;
            Config.OutputChannels.Value = outputChannels > 0 ? outputChannels : input?.Format.Channels ?? 0;
            Config.Input.Value = input;

            if (Config.Input.HasChanged && input!=null)
            {
                var inputChannels = input.Format.Channels;
                if (Config.OutputChannels.Value == 0)
                {
                    Config.OutputChannels.Value = inputChannels;
                }

                Output.Format = input.Format.WithChannels(Config.OutputChannels.Value);
                _logger.Information("Creating Buffer & Processor for Loop {@InputFormat} => {@OutputFormat}",
                    input.Format, Output.Format);
            }

            return Update();
        }

        public override bool IsInputValid(AudioLoopRegionConfig next)
        {
            return next.Input.Value!=null && 
                   next.OutputChannels.Value>0 && 
                   next.Input.Value.Format.Channels>0 && 
                   next.Input.Value.Format.SampleCount>0;
        }

        public override Task<bool> Create(AudioLoopRegionConfig config)
        {
            if (_processor != null)
            {
                _logger.Warning("TransformBlock != null!");
            }
            _processor = new TransformBlock<AudioDataMessage, AudioDataMessage>(input =>
            {
                if (Config.Bypass.Value)
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

                    var inputBufferSize = Config.Input.Value.Format.BufferSize;
                    if (input.BufferSize != inputBufferSize)
                    {
                        throw new Exception($"Expected Input size: {inputBufferSize}, actual: {input.BufferSize}");
                    }

                    var samples = Config.Input.Value.Format.SampleCount;
                    var outputBufferSize = Output.Format.BufferSize;
                    var outputChannels = Output.Format.Channels;
                    var inputChannels = Config.Input.Value.Format.Channels;

                    _clock.Init(input.Time.DTime);

                    if (_state != null && _updateFunc != null)
                    {
                        channels.Update(output.Data, input.Data, outputChannels, inputChannels);
                        var increment = 1.0d / Config.Input.Value.Format.SampleRate;
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
            });
           

            return Task.FromResult(true);
        }

        public override Task<bool> Free()
        {
            if (_processor == null)
            {
                _logger.Error("TransformBlock == null!");
                return Task.FromResult(false);
            }
            _processor.Complete();
            return _processor.Completion.ContinueWith((t)=>
            {
                _processor = null;
                _logger.Information("Transform block stopped, status={status}", t.Status);
                return true;
            });
        }

        public override bool Start()
        {
            Output.SourceBlock = _processor;
            _inputBufferLink = InputBufferBlock.LinkTo(_processor);

            return true;
        }

        public override Task<bool> Stop()
        {
            _inputBufferLink.Dispose();
            Output.SourceBlock = null;
            return Task.FromResult(true);
        }
        

        public override string DebugInfo()
        {
            return $"LOOP [{_processor?.InputCount}/{_processor?.OutputCount}, {_processor?.Completion.Status}]";
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