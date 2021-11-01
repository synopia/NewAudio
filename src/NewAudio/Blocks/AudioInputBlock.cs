using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Irony.Parsing;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Blocks
{
    public class AudioInputBlock : AudioBufferBlock
    {
        private ActionBlock<AudioDataRequestMessage> Processor;
        protected override ITargetBlock<AudioDataMessage> DataResponseSource => null;
        protected override ITargetBlock<AudioDataRequestMessage> DataRequestSource => Processor;
        private bool _reusedData;
        protected override bool IsForwardLifecycleMessages => true;

        private PlayPauseStop _playPauseStop;

        public AudioInputBlock(CircularBuffer buffer, AudioDataflow flow, IAudioFormat format,
            PlayPauseStop playPauseStop) : base(buffer, flow, format)
        {
            _playPauseStop = playPauseStop;
            Processor = new ActionBlock<AudioDataRequestMessage>(message =>
            {
                try
                {
                    int remaining = message.RequestedSamples;
                    var header = Buffer.ReadNodeHeader();
                    var dist = header.WriteEnd - header.ReadStart;
                    Logger.Information("DIST {d}", dist);
                    var pos = 0;
                    CancellationToken cancellationToken = playPauseStop.GetToken();
                    var output = new AudioDataMessage(Format, 256);
                    while (pos<8*512 && !cancellationToken.IsCancellationRequested)
                    {
                        var x = Buffer.Read(output.Data, pos%512);
                        pos += x;
                        if (pos%512 == 0)
                        {
                            Source.Post(output);
                            output = new AudioDataMessage(Format, 256);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("{e}", e);
                }
            });
        }

        protected AudioDataMessage CreateMessage(AudioDataRequestMessage request, int sampleCount)
        {
            AudioDataMessage output;
            if (!_reusedData && request.Data != null)
            {
                output = new AudioDataMessage(request.Data, Format, sampleCount / Format.Channels);
                _reusedData = true;
            }
            else
            {
                output = new AudioDataMessage(Format, sampleCount / Format.Channels);
            }

            return output;
        }
    }
}