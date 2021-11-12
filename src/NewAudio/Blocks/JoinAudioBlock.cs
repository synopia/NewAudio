using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;

namespace NewAudio.Blocks
{
    public class JoinAudioBlock : AudioBlock
    {
        private IDisposable _link1;
        private IDisposable _link2;
        public int? OutputBufferCount => _joinOneAndTwo?.OutputCount;

        public override ISourceBlock<AudioDataMessage> Source { get; set; }
        public override ITargetBlock<AudioDataMessage> Target { get; set; }

        private JoinBlock<AudioDataMessage, AudioDataMessage> _joinOneAndTwo;

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
                    if (input.Item1.BufferSize != sampleCount * channels1)
                    {
                        Logger.Error("Expected Input size: {InputSize}, actual: {ActualSize}", sampleCount * channels1,
                            input.Item1.BufferSize);
                    }

                    if (input.Item2.BufferSize != sampleCount * channels2)
                    {
                        Logger.Error("Expected Input size: {InputSize}, actual: {ActualSize}", sampleCount * channels2,
                            input.Item2.BufferSize);
                    }

                    var time1 = input.Item1.Time;
                    var time2 = input.Item2.Time;
                    if (time1 != time2)
                    {
                        Logger.Warning("TIME DIFF {Time1}!={Time2}", time1, time2);
                    }

                    var buf = new AudioDataMessage(outputFormat, sampleCount)
                    {
                        Time = time1 // todo
                    };
                    for (var i = 0; i < sampleCount; i++)
                    {
                        for (var c = 0; c < channels1; c++)
                        {
                            buf.Data[i * channels + c] = input.Item1.Data[i * channels1 + c];
                        }

                        for (var c = 0; c < channels2; c++)
                        {
                            buf.Data[i * channels + channels1 + c] = input.Item2.Data[i * channels2 + c];
                        }
                    }

                    output.Post(buf);
                }, new ExecutionDataflowBlockOptions()
                {
                    // todo
                });

                _joinOneAndTwo = joinOneAndTwo;
                _link1 = one.SourceBlock.LinkTo(_joinOneAndTwo.Target1);
                _link2 = two.SourceBlock.LinkTo(_joinOneAndTwo.Target2);

                _joinOneAndTwo.LinkTo(oneAndTwoAction);

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