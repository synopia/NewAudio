using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;

namespace NewAudio.Nodes
{
    public class AudioMixerParams : AudioParams
    {
        public AudioParam<AudioLink> Input2;
    }
    public class AudioMixer: AudioNode
    {
        private IDisposable _link2;
        private IDisposable _link1;
        private Task _sender;
        private CancellationTokenSource _tokenSource;
        public override string NodeName => "Mixer";
        public AudioMixerParams Params { get; }

        public AudioMixer()
        {
            InitLogger<AudioMixer>();
            Params = AudioParams.Create<AudioMixerParams>();
            Logger.Information("Audio mixer created");
        }

        public AudioLink Update(AudioLink input, AudioLink two, int bufferSize=4)
        {
            Params.Input2.Value = two;
            PlayParams.Update(input, Params.HasChanged, bufferSize);

            return Update(Params);
        }

        public override bool Play()
        {
            if (PlayParams.Input.Value != null && Params.Input2.Value != null)
            {
                var input1Channels = PlayParams.InputFormat.Value.Channels;
                var input2Channels = Params.Input2.Value.Format.Channels;
                var totalChannels = input1Channels + input2Channels;
                var outFormat = PlayParams.InputFormat.Value.WithChannels(totalChannels);
                var mixer = new MixBuffers(2, 2, outFormat);

                _tokenSource = new CancellationTokenSource();
                var token = _tokenSource.Token;
                var count1 = 0;
                var count2 = 0;
                var action1 = new ActionBlock<AudioDataMessage>(msg =>
                {
                    IMixBuffer buf = null;
                    try
                    {
                        while (buf == null)
                        {
                            buf = mixer.GetWriteBuffer(token);
                            buf.WriteChannelsInterleaved(0, input1Channels, msg.Data);
                        }
                    }
                    catch (OperationCanceledException e)
                    {
                    }

                    ArrayPool<float>.Shared.Return(msg.Data);
                    count1++;
                }, new ExecutionDataflowBlockOptions()
                {
                    BoundedCapacity = 1,
                    MaxDegreeOfParallelism = 1,
                    CancellationToken = token,
                    SingleProducerConstrained = true,
                    MaxMessagesPerTask = 1,
                });
                var action2 = new ActionBlock<AudioDataMessage>(msg =>
                {
                    IMixBuffer buf = null;
                    try
                    {
                        while (buf == null)
                        {
                            buf = mixer.GetWriteBuffer(token);
                            buf.WriteChannelsInterleaved(input1Channels, input2Channels, msg.Data);
                        }
                    }
                    catch (OperationCanceledException e)
                    {
                    }

                    count2++;
                    ArrayPool<float>.Shared.Return(msg.Data);
                }, new ExecutionDataflowBlockOptions()
                {
                    BoundedCapacity = 1,
                    MaxDegreeOfParallelism = 1,
                    CancellationToken = token,
                    SingleProducerConstrained = true,
                    MaxMessagesPerTask = 1,

                });
                var buffer = new BufferBlock<AudioDataMessage>(new DataflowBlockOptions()
                {
                    BoundedCapacity = 2
                    
                });
                
                _sender = Task.Run(() =>
                {
                    var count = 0;
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            var buf = mixer.GetReadBuffer(token);
                            if (buf != null)
                            {
                                var msg = new AudioDataMessage(buf.GetFloatArray(), outFormat,
                                    PlayParams.InputFormat.Value.SampleCount);
                                buffer.Post(msg);
                                count++;
                            }
                        }
                    }
                    catch (OperationCanceledException e)
                    {
                        
                    }
                }, token);
                _link1 = PlayParams.Input.Value.SourceBlock.LinkTo(action1);
                _link2 = Params.Input2.Value.SourceBlock.LinkTo(action2);
                Output.SourceBlock = buffer;
                Output.Format = outFormat;
                return true;
            }

            return false;
        }

        public override void Stop()
        {
            _tokenSource?.Cancel();
            _link1?.Dispose();
            _link2?.Dispose();
            Output.SourceBlock = null;
        }
        
        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _tokenSource?.Cancel();
                    _link1?.Dispose();
                    _link2?.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}