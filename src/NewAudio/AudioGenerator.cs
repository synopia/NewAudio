using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Internal;

namespace NewAudio
{
    public class AudioGenerator: AudioNodeProducer
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioGenerator");
        
        private AudioFlowBuffer _buffer;
        private IDisposable _link;
        private IDisposable _requestLink;

        private readonly BufferBlock<AudioBuffer> _bufferIn =
            new BufferBlock<AudioBuffer>(new DataflowBlockOptions()
            {
                BoundedCapacity = 2,
                MaxMessagesPerTask = 2
            });

        private ActionBlock<int> _worker;

        public AudioGenerator()
        {
        }

        public AudioLink ChangeSettings(AudioSampleRate sampleRate = AudioSampleRate.Hz44100,
            int channels = 2, int bufferSize = 512, int blockCount = 16)
        {
            Stop();
            var format = new AudioFormat(channels, (int)sampleRate, bufferSize);
            _buffer = new AudioFlowBuffer(format, bufferSize*blockCount, bufferSize);

            _link = _bufferIn.LinkTo(_buffer);
            Output.SourceBlock = _buffer;
            Output.Format = format; 
            
            _logger.Info($"Started generating, format: {Output.Format}");
            var silence = new SilenceProvider(Output.WaveFormat);

            _worker = new ActionBlock<int>(count =>
            {
                var buffer = AudioCore.Instance.BufferFactory.FromSampleProvider(silence, count);
                _bufferIn.Post(buffer);
            });
            _requestLink = AudioCore.Instance.Requests.LinkTo(_worker);

            return Output;
        }

        public void Stop()
        {
            _requestLink?.Dispose();
            _link?.Dispose();
            _worker?.Complete();
            _buffer?.Dispose();
            _worker = null;
            _requestLink = null;
        }

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }
    }
}