using System;
using System.Buffers;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Blocks
{
    public class JoinAudioBlock : AudioBlock
    {
        private readonly ILogger _logger = AudioService.Instance.Logger.ForContext<JoinAudioBlock>();
        private IDisposable _link1;
        private IDisposable _link2;

        public JoinAudioBlock()
        {
        }

        public void Create(AudioLink one, AudioLink two)
        {
            _link1?.Dispose();
            _link2?.Dispose();

            if (one == null && two == null)
            {
                return;
            }

            if (one != null && two != null)
            {
                var format = one.Format;
                var channels1 = one.Format.Channels;
                var channels2 = two.Format.Channels;
                var channels = channels1 + channels2;
                var sampleCount = format.SampleCount;
            
                var output = new BufferBlock<AudioDataMessage>();
                var outputFormat = format.WithChannels(channels);
            
                var joinOneAndTwo = new JoinBlock<AudioDataMessage, AudioDataMessage>(new GroupingDataflowBlockOptions()
                {
                    Greedy = true
                });
                
                var oneAndTwoAction = new ActionBlock<Tuple<AudioDataMessage, AudioDataMessage>>(input =>
                {
                    if (input.Item1.BufferSize != sampleCount*channels1)
                    {
                        _logger.Error("Expected Input size: {inputSize}, actual: {actualSize}", sampleCount*channels1, input.Item1.BufferSize);
                    }
                    if (input.Item2.BufferSize != sampleCount*channels2)
                    {
                        _logger.Error("Expected Input size: {inputSize}, actual: {actualSize}", sampleCount*channels2, input.Item2.BufferSize);
                    }

                    var time1 = input.Item1.Time;
                    var time2 = input.Item2.Time;
                    if (time1 != time2)
                    {
                        _logger.Warning("TIME DIFF {time1}!={time2}", time1, time2);
                    }
                    var buf = new AudioDataMessage(outputFormat, sampleCount)
                    {
                        Time = time1 // todo
                    };
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

                OutputFormat = outputFormat;
                Source = output;
            }
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _link1?.Dispose();
                    _link2?.Dispose();
                    _link1 = null;
                    _link2 = null;
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}