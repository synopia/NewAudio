using System;
using System.Threading.Tasks.Dataflow;
using Serilog;

namespace NewAudio
{
    public class SinGen : AudioNodeTransformer
    {
        private readonly ILogger _logger = Log.ForContext<SinGen>();
        private int _frequency;
        private double _phase;
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

                        double time = 0;
                        if (_useIntTime)
                        {
                            time = inp.Time.Time;
                        }
                        else
                        {
                            time = inp.Time.DTime;
                        }

                        for (int i = 0; i < inp.Count / input.Format.Channels; i += input.Format.Channels)
                        {
                            _phase += _frequency * 2.0 * Math.PI / input.Format.SampleRate;
                            if (_phase > 2.0 * Math.PI)
                            {
                                _phase -= 2.0 * Math.PI;
                            }

                            if (_phase < 0.0)
                            {
                                _phase += 2.0 * Math.PI;
                                
                            }
                            
                            var r = Math.Sin(_phase ) * 0.1;
                            for (int c = 0; c < input.Format.Channels; c++)
                            {
                                target.Data[i  + c] = (float)r;
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