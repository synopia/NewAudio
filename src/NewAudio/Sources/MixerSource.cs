using System;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Sources
{
    public class MixerSource : AudioSourceBase
    {
        private int _currentSampleRate;
        private int _currentBufferSize;
        private AudioBuffer _tempBuffer;
        private IAudioSource[] _sources = Array.Empty<IAudioSource>();

        public IAudioSource[] Sources
        {
            get => _sources;
            set
            {
                int sampleRate;
                int bufferSize;
                sampleRate = _currentSampleRate;
                bufferSize = _currentBufferSize;


                if (sampleRate > 0)
                {
                    foreach (var source in value)
                    {
                        if (Array.IndexOf(_sources, source) == -1)
                        {
                            source.PrepareToPlay(sampleRate, bufferSize);
                        }
                    }
                }

                foreach (var source in _sources)
                {
                    if (Array.IndexOf(value, source) == -1)
                    {
                        source.ReleaseResources();
                    }
                }

                _sources = value;
            }
        }

        public MixerSource()
        {
            _tempBuffer = new AudioBuffer();
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            _tempBuffer.SetSize(2, framesPerBlockExpected);
            _currentBufferSize = framesPerBlockExpected;
            _currentSampleRate = sampleRate;
            foreach (var source in _sources)
            {
                source.PrepareToPlay(sampleRate, framesPerBlockExpected);
            }
        }

        public override void ReleaseResources()
        {
            foreach (var source in _sources)
            {
                source.ReleaseResources();
            }

            _tempBuffer.SetSize(2, 0);
            _currentBufferSize = 0;
            _currentSampleRate = 0;
        }

        public override void FillNextBuffer(AudioBufferToFill buffer)
        {
            using var s = new ScopedMeasure("MixerSource.GetNextAudioBlock");
            if (_sources.Length > 0)
            {
                _sources[0].FillNextBuffer(buffer);
                if (_sources.Length > 1)
                {
                    var numFrames = buffer.NumFrames;
                    _tempBuffer.SetSize(Math.Max(1, buffer.Buffer.NumberOfChannels), numFrames);
                    var buf = new AudioBufferToFill(_tempBuffer, 0, numFrames);
                    for (var i = 1; i < _sources.Length; i++)
                    {
                        _sources[i].FillNextBuffer(buf);
                        for (var ch = 0; ch < buffer.Buffer.NumberOfChannels; ch++)
                        {
                            buffer.Buffer[ch].Offset(buffer.StartFrame)
                                .Add(_tempBuffer[ch], numFrames);
                        }
                    }
                }
            }
            else
            {
                buffer.ClearActiveBuffer();
            }
        }
    }
}