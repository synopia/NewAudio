using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Block;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Core
{
    public class AudioLink : IDisposable
    {
        private readonly IResourceHandle<AudioGraph> _graph;

        public AudioBlock AudioBlock;
        public AudioFormat Format { get; set; }

        public AudioLink() 
        {
            _graph = Factory.GetAudioGraph();
        }


        public void Dispose()
        {
            Dispose(true);
        }

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    
                }

                _disposedValue = disposing;
            }
        }
    }
}