using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;

namespace NewAudio.Nodes
{
    public class AudioBufferOut : BaseNode<AudioEffectBlock>
    {
        private float[] _outBuffer;
        private CircularSampleBuffer _sampleBuffer;
        public int BufferSize { get; private set; }
        public override AudioEffectBlock AudioBlock { get; }

        public AudioBufferOut()
        {
            var readBuffer = new ActionBlock<IAudioDataMessage>(input =>
            {
                var inputBufferSize = Math.Min(input.BufferSize, BufferSize);
                if (_sampleBuffer == null || _sampleBuffer.MaxLength != inputBufferSize)
                {
                    _sampleBuffer = new CircularSampleBuffer(inputBufferSize)
                    {
                        BufferFilled = data =>
                        {
                            if (_outBuffer != null)
                            {
                                Array.Copy(data, 0, _outBuffer, 0, inputBufferSize);
                            }
                        }
                    };
                }

                if (_outBuffer == null || _outBuffer.Length != inputBufferSize)
                {
                    _outBuffer = new float[inputBufferSize];
                }

                var written = _sampleBuffer.Write(input.Data, 0, inputBufferSize);
                _sampleBuffer.Advance(written);
            });

            
            
            Connect += input =>
            {
                var filter = new MessageFilterBlock<IAudioDataMessage>();
                input.SourceBlock.LinkTo(filter);
                filter.LinkTo(readBuffer);
            };
            Disconnect += input =>
            {

            };
            Reconnect += (o, n) =>
            {
                Connect?.Invoke(n);
            };
            // Output.SourceBlock = readBuffer;
        }
        
        public IEnumerable Update()
        {
            if (_outBuffer!=null)
            {
                return _outBuffer;
            }            
            return Enumerable.Empty<float>();
        }
        

    
        
        public override void Dispose()
        {
            base.Dispose();
        }
    }
}