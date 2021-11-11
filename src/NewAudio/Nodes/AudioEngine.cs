﻿using System;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public class AudioEngine : IDisposable
    {
        private readonly IResourceHandle<AudioGraph> _graph; 
        
        public AudioEngine()
        {
            _graph = VLApi.Instance.GetAudioGraph();
        }
        
        public void Update(bool playing, int bufferSize = 512, int buffersCount = 6)
        {
            _graph.Resource.Update(playing, bufferSize, buffersCount);
        }

        public void Dispose()
        {
            _graph.Dispose();
        }
    }
}