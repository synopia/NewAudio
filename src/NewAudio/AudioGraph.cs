using System.Collections.Generic;

namespace NewAudio
{
    public class AudioGraph
    {
        private readonly List<AudioLink> _links = new List<AudioLink>();
        private readonly List<AudioNodeSink> _sinks = new List<AudioNodeSink>();
        private readonly List<AudioNodeInput> _inputs = new List<AudioNodeInput>();

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

        public override string ToString()
        {
            return $"{_links.Count}, sinks: {_sinks.Count}, sources: {_inputs.Count}";
        }

        public void Dispose()
        {
            _links.Clear();
            _inputs.Clear();
            _sinks.Clear();
        }
    }
}