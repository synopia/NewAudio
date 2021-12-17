using System;
using System.Threading;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Files
{
    public class AudioFileReaderSource: AudioSourceNode, IPositionalAudioSource
    {
        
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

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            using var s = new ScopedMeasure("AudioFileReaderSource.GetNextAudioBlock");
            var start = _nextPosition;
            var numFrames = bufferToFill.NumFrames;
            if (IsLooping)
            {
                var newStart = start % TotalLength;
                var newEnd = (start+numFrames) % TotalLength;
                
                if (newStart <= newEnd)
                {
                    _fileReader.Read(bufferToFill, newStart);
                }
                else
                {
                    var firstEnd = (int)(TotalLength - newStart);
                    
                    _fileReader.Read(new AudioSourceChannelInfo(bufferToFill.Buffer, 0, firstEnd), newStart);
                    _fileReader.Read(new AudioSourceChannelInfo(bufferToFill.Buffer, firstEnd, (int)newEnd), 0);
                }

                _nextPosition = newEnd;
            }
            else
            {
                _fileReader.Read(bufferToFill, start);
                _nextPosition += numFrames;
            }

        }

    }
}