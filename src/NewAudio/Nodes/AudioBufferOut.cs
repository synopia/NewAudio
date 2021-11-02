using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;

namespace NewAudio.Nodes
{
    public class AudioBufferOut : BaseNode
    {
        private readonly ILogger _logger;
        private float[] _outBuffer;
        private readonly ActionBlock<AudioDataMessage> _readBuffer;

        public AudioBufferOut()
        {
            _logger = AudioService.Instance.Logger.ForContext<OutputDevice>();
            _logger.Information("AudioBufferOut created");
            _readBuffer = new ActionBlock<AudioDataMessage>(input =>
            {
                var inputBufferSize = Math.Min(input.BufferSize, BufferSize);
                // if (_sampleBuffer == null || _sampleBuffer.MaxLength != inputBufferSize)
                    // _sampleBuffer = new CircularSampleBuffer(inputBufferSize)
                    // {
                        // BufferFilled = data =>
                        // {
                            // if (_outBuffer != null) Array.Copy(data, 0, _outBuffer, 0, inputBufferSize);
                        // }
                    // };

                if (_outBuffer == null || _outBuffer.Length != inputBufferSize) _outBuffer = new float[inputBufferSize];

                // var written = _sampleBuffer.Write(input.Data, 0, inputBufferSize);
                // _sampleBuffer.Advance(written);
            });


            OnConnect += link =>
            {
                _logger.Information("New connection");
                AddLink(link.SourceBlock.LinkTo(_readBuffer));
            };
            OnDisconnect += link =>
            {
                DisposeLinks();
                _logger.Information("Disconnected from");
            };
            // Output.SourceBlock = readBuffer;
        }

        public int BufferSize { get; private set; }
        protected override bool IsInputValid(AudioLink link)
        {
            return true;
        }

        protected override void Start()
        {
        }

        protected override void Stop()
        {
        }

        public IEnumerable Update()
        {
            if (_outBuffer != null) return _outBuffer;
            return Enumerable.Empty<float>();
        }


        public override void Dispose()
        {
            base.Dispose();
        }
    }
}