using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VL.Lib.Collections;
using SilenceProvider = NewAudio.Internal.SilenceProvider;

namespace NewAudio
{
    /// <summary>Allows any number of inputs to be patched to outputs</summary>
    /// <remarks>Remark?</remarks>
    /// 
    public class Multiplexer : AudioNodeTransformer
    {
        private readonly Logger _logger = LogFactory.Instance.Create("Multiplexer");
        private List<AudioLink> _inputs;
        private List<int> _outputMap;
        private int _inputChannels;
        private IDisposable _link1;
        private IDisposable _link2;

        public Multiplexer()
        {
        }


        public void Join(AudioLink one, AudioLink two)
        {
            if (_link1 != null)
            {
                _link1.Dispose();
            }

            if (_link2 != null)
            {
                _link2.Dispose();
            }

            if (one == null || two == null)
            {
                return;
            }
            var format = one.Format;
            var channels1 = one.Format.Channels;
            var channels2 = two.Format.Channels;
            var channels = channels1 + channels2;
            var sampleCount = format.SampleCount;
            
            var output = new BufferBlock<AudioBuffer>();
            var outputFormat = format.WithChannels(channels);
            
            var joinOneAndTwo = new JoinBlock<AudioBuffer, AudioBuffer>(new GroupingDataflowBlockOptions()
            {
                Greedy = true
            });
            var oneAndTwoAction = new ActionBlock<Tuple<AudioBuffer, AudioBuffer>>(input =>
            {
                if (input.Item1.Count != sampleCount*channels1)
                {
                    _logger.Error($"Expected Input size: {sampleCount*channels1}, actual: {input.Item1.Count}");
                }
                if (input.Item2.Count != sampleCount*channels2)
                {
                    _logger.Error($"Expected Input size: {sampleCount*channels2}, actual: {input.Item2.Count}");
                }

                var time1 = input.Item1.Time;
                var time2 = input.Item2.Time;
                if (time1 != time2)
                {
                    _logger.Warn($"TIME DIFF {time1}!={time2}");
                }
                var buf = AudioCore.Instance.BufferFactory.GetBuffer(Math.Max(time1, time2), channels * sampleCount);
                for (int i = 0; i < sampleCount; i++)
                {
                    for (int c = 0; c < channels1; c++)
                    {
                        buf.Data[i * channels + c] = input.Item1.Data[i * channels1 + c];
                    }
                    for (int c = 0; c < channels2; c++)
                    {
                        buf.Data[i * channels + channels1 + c] = input.Item2.Data[i * channels2 + c];
                    }
                }
                output.Post(buf);
            });
            
            _link1 = one.SourceBlock.LinkTo(joinOneAndTwo.Target1);
            _link2 = two.SourceBlock.LinkTo(joinOneAndTwo.Target2);
            
            joinOneAndTwo.LinkTo(oneAndTwoAction);

            Output.Format = outputFormat;
            Output.SourceBlock = output;
        }
        /*
        private AudioLink[] _lastInputs;
        private int[] _lastOutputMap;

        public void ChangeSettings(Spread<AudioLink> inputs, Spread<int> outputMap)
        {
            var inputEquals = Utils.ArrayEquals(inputs.ToArray(), _lastInputs);
            var outputMapEquals = Utils.ArrayEquals(outputMap.ToArray(), _lastOutputMap);
            if (inputEquals && outputMapEquals)
            {
                return;
            }

            _lastInputs = inputs.ToArray();
            _lastOutputMap = outputMap.ToArray();

            _inputs = inputs.ToList();
            if (_inputs.Any(i => i == null) || _inputs.Count==0 )
            {
                return;
            }
            _inputChannels = _inputs.Sum(i => i.WaveFormat.Channels);
            if (outputMap.Count == 0)
            {
                InitOutputMap();
            }
            else
            {
                _outputMap = outputMap.ToList();
            }

            var outputChannels = _outputMap.Count;
            Format = _inputs[0].Format.WithChannels(outputChannels);
            _logger.Info($"InputCount: {_inputChannels} OutputCount: {outputChannels}");
            var multiplexer = new MultiplexingSampleProvider(_inputs, outputChannels);
            for (int i = 0; i < outputChannels; i++)
            {
                var input = _outputMap[i];
                multiplexer.ConnectInputToOutput(input, i);
                _logger.Info($" ch: {input} => {i}");
            }

            Output.Format = Format;
            var samples = Format.BufferSize;
            var outputBuffer = new AudioFlowBuffer(Format, Format.BufferSize * outputChannels);
            for(int index=0; index<inputs.Count; index++)
            {
                inputs[index].BroadcastBlock.LinkTo()
                var b = new ActionBlock<AudioBuffer>(inputBuffer =>
                {
                    for (int i = 0; i < samples; i+=inputs[x].Format.Channels)
                    {
                        outputBuffer.Buffer.Data[i*outputChannels]
                    }
                });
                i.SourceBlock.LinkTo(b);
            });
            
            Output.SourceBlock = new TransformBlock<AudioBuffer, AudioBuffer>(input =>
                AudioCore.Instance.BufferFactory.FromSampleProvider(multiplexer, Format.BufferSize));
            
        }

        private void InitOutputMap()
        {
            _outputMap = new List<int>();
            for (int i = 0; i < _inputChannels; i++)
            {
                _outputMap.Add(i);
            }
        }
        */

        public override void Dispose()
        {
        }
    }
}