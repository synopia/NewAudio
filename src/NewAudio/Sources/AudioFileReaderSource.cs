using System;
using System.Threading;
using Serilog;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Files
{
    public class AudioFileReaderSource: AudioSourceBase, IPositionalAudioSource
    {
        private ILogger _logger = Resources.GetLogger<AudioFileReaderSource>();
        
        public long NextReadPos
        {
            get => IsLooping  ? _nextPosition % TotalLength : _nextPosition;
            set => _nextPosition = value;
        }

        public long TotalLength => _fileReader.Samples;
        public bool IsLooping { get; set; }
        private long _nextPosition;
        private IAudioFileReader _fileReader;
        
        public AudioFileReaderSource(IAudioFileReader fileReader)
        {
            _fileReader = fileReader;
        }
        
        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            
        }

        public override void ReleaseResources()
        {
        }

        public override void FillNextBuffer(AudioBufferToFill buffer)
        {
            using var s = new ScopedMeasure("AudioFileReaderSource.GetNextAudioBlock");
            var start = _nextPosition;
            var numFrames = buffer.NumFrames;
            if (IsLooping)
            {
                var newStart = start % TotalLength;
                var newEnd = (start+numFrames) % TotalLength;
                
                if (newStart < newEnd)
                {
                    _fileReader.Read(buffer, newStart);
                }
                else
                {
                    var firstEnd = (int)(TotalLength - newStart);
                    
                    _fileReader.Read(new AudioBufferToFill(buffer.Buffer, 0, firstEnd), newStart);
                    _fileReader.Read(new AudioBufferToFill(buffer.Buffer, firstEnd, (int)newEnd), 0);
                }

                _nextPosition = newEnd;
            }
            else
            {
                _fileReader.Read(buffer, start);
                _nextPosition += numFrames;
            }

        }

    }
}