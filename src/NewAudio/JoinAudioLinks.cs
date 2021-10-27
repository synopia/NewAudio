using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;
using VL.Lib.Collections;
using SilenceProvider = NewAudio.Internal.SilenceProvider;

namespace NewAudio
{
    /// <summary></summary>
    /// <remarks></remarks>
    /// 
    public class JoinAudioLinks : AudioNodeTransformer
    {
        private readonly ILogger _logger = Log.ForContext<JoinAudioLinks>();
        private IDisposable _link1;
        private IDisposable _link2;

        public JoinAudioLinks()
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
                    _logger.Error("Expected Input size: {inputSize}, actual: {actualSize}", sampleCount*channels1, input.Item1.Count);
                }
                if (input.Item2.Count != sampleCount*channels2)
                {
                    _logger.Error("Expected Input size: {inputSize}, actual: {actualSize}", sampleCount*channels2, input.Item2.Count);
                }

                var time1 = input.Item1.Time;
                var time2 = input.Item2.Time;
                if (time1 != time2)
                {
                    _logger.Warning("TIME DIFF {time1}!={time2}", time1, time2);
                }
                var buf = AudioCore.Instance.BufferFactory.GetBuffer(channels * sampleCount);
                buf.Time = Math.Max(time1, time2);
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

        public override void Dispose()
        {
            _link1?.Dispose();
            _link2?.Dispose();
            base.Dispose();
        }
    }
}