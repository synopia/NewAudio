using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Block;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Core
{
    public class AudioPin : AudioBlock
    {
        public override string Name => "Output Pin";

        public AudioPin() : base(new AudioBlockFormat(){ChannelMode = ChannelMode.MatchesInput, AutoEnable = true})
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
            _graph = Factory.GetAudioGraph();
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
                    
                }

                _disposedValue = disposing;
            }
        }
    }
}