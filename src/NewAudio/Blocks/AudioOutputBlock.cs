using System.Threading;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Blocks
{
    public class AudioOutputBlock : AudioBufferBlock 
    {
        protected ActionBlock<AudioDataMessage> Processor;
        protected override ITargetBlock<AudioDataMessage> DataResponseSource => Processor;
        protected override ITargetBlock<AudioDataRequestMessage> DataRequestSource => null;
        protected  override bool IsForwardLifecycleMessages => false;

        private PlayPauseStop _playPauseStop;
        public AudioOutputBlock(CircularBuffer buffer, AudioDataflow flow, IAudioFormat format, PlayPauseStop playPauseStop) : base(buffer, flow, format)
        {
            _playPauseStop = playPauseStop;
            Processor = new ActionBlock<AudioDataMessage>(message =>
            {
                var token = _playPauseStop.GetToken();
                Logger.Verbose("Writing data to Main Buffer Out {message} {size} {token}",message.Data.Length, message.BufferSize, token.IsCancellationRequested);

                var pos = 0;
                while (pos<message.BufferSize && !token.IsCancellationRequested)
                {
                    var v = Buffer.Write(message.Data, pos);
                    pos += v;
                }
            });
            
        }
      
    }
}