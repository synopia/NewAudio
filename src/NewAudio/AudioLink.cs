using System;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using Serilog;

namespace NewAudio
{
    /// <summary>
    /// Represents an output pin, where audio data is sent to.
    /// Internally uses a BroadcastBlock, to dispatch incoming audio to multiple targets. 
    /// </summary>
    public class AudioLink : IDisposable, ISampleProvider
    {
        private readonly ILogger _logger = Log.ForContext<AudioLink>();
        public WaveFormat WaveFormat => Format.WaveFormat;
        public AudioFormat Format;

        private readonly BroadcastBlock<AudioBuffer> _broadcastBlock = new BroadcastBlock<AudioBuffer>(i=>i, new GroupingDataflowBlockOptions()
        {
        });

        private ISourceBlock<AudioBuffer> _sourceBlock;
        private IDisposable _outputLink;
        public ISourceBlock<AudioBuffer> SourceBlock
        {
            get=>_broadcastBlock;
            set
            {
                _outputLink?.Dispose();
                _sourceBlock = value;
                _outputLink = _sourceBlock?.LinkTo(_broadcastBlock);
            }
        }
        
        public AudioLink()
        {
            AudioCore.Instance.AudioGraph.AddAudioLink(this);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        
        

        public void Dispose()
        {
            AudioCore.Instance.AudioGraph.RemoveAudioLink(this);
        }
    }
}