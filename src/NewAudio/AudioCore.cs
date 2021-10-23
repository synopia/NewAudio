using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;

namespace NewAudio
{
    public enum AudioSampleRate
    {
        Hz8000 = 8000,
        Hz11025 = 11025,
        Hz16000 = 16000,
        Hz22050 = 22050,
        Hz32000 = 32000,
        Hz44056 = 44056,
        Hz44100 = 44100,
        Hz48000 = 48000,
        Hz88200 = 88200,
        Hz96000 = 96000,
        Hz176400 = 176400,
        Hz192000 = 192000,
        Hz352800 = 352800
    }

    public class AudioCore
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioEngine");
        private static AudioCore _instance;

        public static AudioCore Instance => _instance ??= new AudioCore();

        public readonly AudioBufferFactory BufferFactory = new AudioBufferFactory();
        
        private readonly List<AudioLink> _links = new List<AudioLink>();
        private readonly List<AudioNodeSink> _sinks = new List<AudioNodeSink>();
        private readonly List<AudioNodeInput> _inputs = new List<AudioNodeInput>();
        public readonly BufferBlock<int> Requests = new BufferBlock<int>();

        public void Init()
        {
            _logger.Info($"AudioEngine started links: {_links.Count}, sinks: {_sinks.Count}, sources: {_inputs.Count}");
        }

        public void AddAudioLink(AudioLink audioLink)
        {
            if (!_links.Contains(audioLink))
            {
                _links.Add(audioLink);
            }
        }

        public void RemoveAudioLink(AudioLink audioLink)
        {
            _links.Remove(audioLink);
        }

        public void AddSink(AudioNodeSink sink)
        {
            if (!_sinks.Contains(sink))
            {
                _sinks.Add(sink);
            }
        }

        public void RemoveSink(AudioNodeSink sink)
        {
            _sinks.Remove(sink);
        }

        public void AddInput(AudioNodeInput source)
        {
            if (!_inputs.Contains(source))
            {
                _inputs.Add(source);
            }
        }

        public void RemoveInput(AudioNodeInput source)
        {
            _inputs.Remove(source);
        }

        public void Update(out int bufferCacheSize, bool reset = false)
        {
            if (reset)
            {
                BufferFactory.Clear();
            }

            bufferCacheSize = BufferFactory.Count;
        }

        private int Read(int offset, int count)
        {
            foreach (var sample in _links)
            {
            }

            // foreach (var output in _sources)
            // {
            // output.Read(offset, count);
            // }

            float[] buffer = new float[count];
            var total = 0;
            foreach (var sink in _sinks)
            {
                // total = sink.Read(buffer, offset, count);
            }

            foreach (var sample in _links)
            {
            }

            return total;
        }

    }
}