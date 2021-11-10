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
    public class AudioLoopRegionInitParams : AudioNodeInitParams
    {
   
    }
    public class AudioLoopRegionPlayParams : AudioNodePlayParams
    {
        public AudioParam<bool> Bypass;
        public AudioParam<int> OutputChannels;
    }
    
    public class AudioLoopRegion<TState> : AudioNode<AudioLoopRegionInitParams, AudioLoopRegionPlayParams> where TState : class
    {
        private readonly ILogger _logger;
        private TransformBlock<AudioDataMessage, AudioDataMessage> _processor;
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
            
            int outputChannels = 0,
            int bufferSize = 4
        )
        {
            if (_state == null && create != null)
            {
                _state = create(_clock);
            }
            _updateFunc = update;

            PlayParams.BufferSize.Value = bufferSize;
            PlayParams.Bypass.Value = bypass;
            PlayParams.OutputChannels.Value = outputChannels > 0 ? outputChannels : input?.Format.Channels ?? 0;
            PlayParams.Input.Value = input;

            return Update();
        }

        
        public override bool IsPlayValid()
        {
            return PlayParams.Input.Value!=null && 
                   PlayParams.OutputChannels.Value>0 && 
                   PlayParams.Input.Value.Format.Channels>0 && 
                   PlayParams.Input.Value.Format.SampleCount>0;
        }
        public override bool Play()
        {
            var input = PlayParams.Input.Value;
            var inputChannels = input.Format.Channels;
            if (PlayParams.OutputChannels.Value == 0)
            {
                PlayParams.OutputChannels.Value = inputChannels;
            }

            Output.Format = input.Format.WithChannels(PlayParams.OutputChannels.Value);
            _logger.Information("Creating Buffer & Processor for Loop {@InputFormat} => {@OutputFormat}",
                input.Format, Output.Format);
            
            Output.SourceBlock = _processor;
            TargetBlock = _processor;
            return true;
        }

        public override bool Stop()
        {
            TargetBlock = null;
            Output.SourceBlock = null;
            return true;
        }


        public override Task<bool> Init()
        {
            if (_processor != null)
            {
                _logger.Warning("TransformBlock != null!");
            }

            _processor = new TransformBlock<AudioDataMessage, AudioDataMessage>(input =>
            {
                if (PlayParams.Bypass.Value)
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
                    var outputBufferSize = Output.Format.BufferSize;
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
        
        public override string DebugInfo()
        {
            return $"LOOP: [in/out={_processor?.InputCount}/{_processor?.OutputCount}, {base.DebugInfo()}]";
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