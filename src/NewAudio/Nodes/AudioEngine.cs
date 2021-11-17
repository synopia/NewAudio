using System;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public class AudioEngine : IDisposable
    {
        private readonly IResourceHandle<AudioGraph> _graph;

        public AudioEngine()
        {
            _graph = Factory.GetAudioGraph();
        }

        public bool Update(bool playing)
        {
            return  _graph.Resource.Update(playing);
        }

        public string DebugInfo()
        {
            return _graph.Resource.DebugInfo();
        }

        public void Dispose()
        {
            _graph.Dispose();
        }
    }
}