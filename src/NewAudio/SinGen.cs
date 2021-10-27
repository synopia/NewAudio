using System;
using System.Threading.Tasks.Dataflow;
using Serilog;

namespace NewAudio
{
    public class SinGen : AudioNodeTransformer
    {
        private readonly ILogger _logger = Log.ForContext<SinGen>();
        private int _frequency;
        private bool _useIntTime;
        private IDisposable _link;
        private TransformBlock<AudioBuffer, AudioBuffer> _t;

        public SinGen()
        {
        }

        public AudioLink Update(AudioLink input, int frequency, bool useIntTime)
        {
            _frequency = frequency;
            _useIntTime = useIntTime;
            if (input != Input )
            {
                _link?.Dispose();
                Connect(input);
                if (input != null)
                {
                    _frequency = frequency;

                    _t = new TransformBlock<AudioBuffer, AudioBuffer>(inp =>
                    {
                        _logger.Verbose("Received {size} at {time}  in={inBuffers} out={outBuffers}", inp.Count, inp.Time, _t.InputCount, _t.OutputCount);
                        var target = AudioCore.Instance.BufferFactory.GetBuffer(inp.Count);
                        target.Time = inp.Time;
                        target.DTime = inp.DTime;

                        double time = 0;
                        if (_useIntTime)
                        {
                            time = (double)inp.Time / input.Format.SampleRate;
                        }
                        else
                        {
                            time = inp.DTime;
                        }

                        for (int i = 0; i < inp.Count / input.Format.Channels; i += input.Format.Channels)
                        {
                            var r = Math.Sin(time * _frequency * 2.0* Math.PI ) * 0.1;
                            for (int c = 0; c < input.Format.Channels; c++)
                            {
                                target.Data[i * input.Format.Channels + c] = (float)r;
                            }

                            time += 1.0d / input.Format.SampleRate;                                 
                        }

                        return target;
                    }, new ExecutionDataflowBlockOptions()
                    {
                        // SingleProducerConstrained = true,
                        
                    });

                    _link = input.SourceBlock.LinkTo(_t);
                    Output.SourceBlock = _t;
                    Output.Format = input.Format;
                }
            }

            return Output;
        }

        public override void Dispose()
        {
            _link?.Dispose();
            base.Dispose();
        }
    }
    
    
}