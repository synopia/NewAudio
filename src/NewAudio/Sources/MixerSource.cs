using System;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Sources
{
    public class MixerSource: AudioSourceNode
    {
        private int _currentSampleRate;
        private int _currentBufferSize;
        private object _lock = new();
        private AudioBuffer _tempBuffer;
        private IAudioSource[] _sources = Array.Empty<IAudioSource>();

        public IAudioSource[] Sources
        {
            get => _sources;
            set
            {
                int sampleRate;
                int bufferSize;
                lock (_lock)
                {
                    sampleRate = _currentSampleRate;
                    bufferSize = _currentBufferSize;
                }
                
                
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

                lock (_lock)
                {
                    _sources = value;
                }
            }
        }

        public MixerSource()
        {
            _tempBuffer = new AudioBuffer();
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            _tempBuffer.SetSize(2, framesPerBlockExpected);
            lock (_lock)
            {
                _currentBufferSize = framesPerBlockExpected;
                _currentSampleRate = sampleRate;
                foreach (var source in _sources)
                {
                    source.PrepareToPlay(sampleRate, framesPerBlockExpected);
                }
            }
        }

        public override void ReleaseResources()
        {
            lock (_lock)
            {
                foreach (var source in _sources)
                {
                    source.ReleaseResources();
                }
                _tempBuffer.SetSize(2,0);
                _currentBufferSize = 0;
                _currentSampleRate = 0;
            }
        }

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            lock (_lock)
            {
                if (_sources.Length > 0)
                {
                    _sources[0].GetNextAudioBlock(bufferToFill);
                    if (_sources.Length > 1)
                    {
                        var numFrames = bufferToFill.NumFrames;
                        _tempBuffer.SetSize(Math.Max(1,bufferToFill.Buffer.NumberOfChannels), numFrames);
                        var buf = new AudioSourceChannelInfo(_tempBuffer, 0, numFrames);
                        for (int i = 1; i < _sources.Length; i++)
                        {
                            _sources[i].GetNextAudioBlock(buf);
                            for (int ch = 0; ch < bufferToFill.Buffer.NumberOfChannels; ch++)
                            {
                                bufferToFill.Buffer[ch].Slice(bufferToFill.StartFrame).Span.Add(_tempBuffer[ch].Slice(0).Span, numFrames);
                            }
                        }
                    }
                }
                else
                {
                    bufferToFill.ClearActiveBuffer();
                }
            }
        }
    }
}