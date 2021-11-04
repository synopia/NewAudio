using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Internal;
using Serilog;

namespace NewAudio.Nodes
{
    public interface IAudioBufferOutConfig: IAudioNodeConfig{}
    public class AudioBufferOut : IAudioNode<IAudioBufferOutConfig>
    {
        private readonly ILogger _logger;
        private float[] _outBuffer;
        private readonly ActionBlock<AudioDataMessage> _readBuffer;
        private AudioNodeSupport<IAudioBufferOutConfig> _support;
        public int BufferSize { get; private set; }

        public AudioBufferOut()
        {
            _logger = AudioService.Instance.Logger.ForContext<OutputDevice>();
            _logger.Information("AudioBufferOut created");
            _support = new AudioNodeSupport<IAudioBufferOutConfig>(this);
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

                if (_outBuffer == null || _outBuffer.Length != inputBufferSize)
                {
                    _outBuffer = new float[inputBufferSize];
                }

                // var written = _sampleBuffer.Write(input.Data, 0, inputBufferSize);
                // _sampleBuffer.Advance(written);
            });


            // Output.SourceBlock = readBuffer;
        }

        public AudioParams AudioParams => _support.AudioParams;
        public IAudioBufferOutConfig Config => _support.Config;
        public IAudioBufferOutConfig LastConfig => _support.LastConfig;
        public AudioLink Output => _support.Output;

        public bool IsInputValid(IAudioBufferOutConfig next)
        {
            return true;
        }

        public void OnAnyChange()
        {
        }

        public void OnConnect(AudioLink input)
        {
            _logger.Information("New connection");
            _support.AddLink(input.SourceBlock.LinkTo(_readBuffer));
        }

        public void OnDisconnect(AudioLink link)
        {
            _support.DisposeLinks();
            _logger.Information("Disconnected from");
        }

        // protected override bool IsInputValid(AudioLink link)
        // {
            // return true;
        // }

        public void OnStart()
        {
        }

        public void OnStop()
        {
        }

        public void Dispose()
        {
            _support.Dispose();
        }
    }
}