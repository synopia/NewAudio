using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;
using SharedMemory;

namespace NewAudio.Blocks
{
    public abstract class AudioBufferBlock : BaseAudioBlock 
    {
        public CircularBuffer Buffer { get; }
        public IAudioFormat Format { get; }
        
        
        protected AudioBufferBlock(CircularBuffer buffer, AudioDataflow flow, IAudioFormat format):base(flow)
        {
            Format = format;
            Buffer = buffer;
        }


        public override void Dispose()
        {
            base.Dispose();
        }
    }
}