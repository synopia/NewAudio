using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Internal;

namespace NewAudio
{
    public class AudioBufferGenerator: AudioNodeProducer
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioBufferGenerator");
        private readonly AudioBufferFactory _audioBufferFactory = new AudioBufferFactory();
        
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

        public AudioBufferGenerator()
        {
        }

        public AudioLink ChangeSettings(AudioSampleRate sampleRate = AudioSampleRate.Hz44100,
            int channels = 2, int bufferSize = 512, int blockCount = 16)
        {
            Stop();
            _buffer = new AudioFlowBuffer(bufferSize, blockCount, true);
            _audioBufferFactory.Clear();

            _link = _bufferIn.LinkTo(_buffer);
            Output.SourceBlock = _buffer;
            Output.Format = new AudioFormat(channels, (int)sampleRate, bufferSize, blockCount);
            
            _logger.Info($"Started generating, format: {Output.Format}");
            var silence = new SilenceProvider(Output.WaveFormat);

            _worker = new ActionBlock<int>(count =>
            {
                var buffer = _audioBufferFactory.FromSampleProvider(silence, count);
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
            _audioBufferFactory.Dispose();
            base.Dispose();
        }
    }
}