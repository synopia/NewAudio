using System;
using NewAudio.Processor;
using VL.Lib.Basics.Resources;
using NewAudio;

namespace NewAudio.Core
{
    public class AudioPin : AudioProcessor
    {
        public override string Name => "Output Pin";

        public AudioPin() : base(new AudioProcessorConfig(){ChannelMode = ChannelMode.MatchesInput, AutoEnable = true})
        {
        }
    }
    
    public class AudioLink : IDisposable
    {
        private readonly IResourceHandle<AudioGraph> _graph;

        public readonly AudioPin Pin;
        public AudioFormat Format { get; set; }

        public AudioLink() 
        {
            _graph = Resources.GetAudioGraph();
            Pin = new AudioPin();
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
                    Pin.Dispose();
                    _graph.Dispose();
                }

                _disposedValue = disposing;
            }
        }
    }
}