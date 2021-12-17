using System;
using System.Threading;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;

namespace VL.NewAudio.Sources
{
    public class ResamplingAudioSource: AudioSourceNode
    {
        private IAudioSource _source;
        private int _sourceSampleRate;
        private int _targetSampleRate;
        private int _numChannels;

        private object _lock = new();
        
        private AudioBuffer _buffer = new();
        private CDSPResampler[] _resampler = Array.Empty<CDSPResampler>();
        
        private int _bufferPos;
        private int _samplesInBuffer;
        
        
        public ResamplingAudioSource(IAudioSource source, int sourceSampleRate, int numChannels = 2)
        {
            _source = source;
            _numChannels = numChannels;
            _sourceSampleRate = sourceSampleRate;
        }
        
        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            lock (_lock)
            {
                _targetSampleRate = sampleRate;
                var ratio = _sourceSampleRate / (double)_targetSampleRate;
                var scaledFrames = (int)(ratio * framesPerBlockExpected);
                
                _source.PrepareToPlay(_sourceSampleRate, scaledFrames);
                
                _buffer.SetSize(_numChannels, scaledFrames);
                _bufferPos = 0;
                _samplesInBuffer = 0;
                for (int i = 0; i < _resampler.Length; i++)
                {
                    _resampler[i].Dispose();
                }
                _resampler = new CDSPResampler[_numChannels];
                for (int i = 0; i < _resampler.Length; i++)
                {
                    _resampler[i] = new CDSPResampler(_sourceSampleRate, _targetSampleRate, scaledFrames);
                }
            }
        }

        public override void ReleaseResources()
        {
            _source.ReleaseResources();
            for (int i = 0; i < _resampler.Length; i++)
            {
                _resampler[i].Dispose();
            }
            _resampler = Array.Empty<CDSPResampler>();
            _buffer.SetSize(_numChannels,0);
        }

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            using var s = new ScopedMeasure("ResamplingAudioSource.GetNextAudioBlock");
            lock (_lock)
            {
                var start = 0;
                var bufs = bufferToFill.Buffer.GetWriteChannels();
                int[] remaining = new int[_numChannels];
                for (int ch = 0; ch < _numChannels; ch++)
                {
                    remaining[ch] = bufferToFill.NumFrames;
                }
                var finished = false;
                while (!finished)
                {
                    AudioSourceChannelInfo info = new AudioSourceChannelInfo(_buffer, 0, _buffer.NumberOfFrames);
                    _source.GetNextAudioBlock(info);
                    start += _buffer.NumberOfFrames;
                    finished = true;
                    
                    for (int ch = 0; ch < _numChannels; ch++)
                    {
                        for (int i = 0; i < _buffer.NumberOfFrames; i++)
                        {
                            _resampler[ch].InData[i] = _buffer[ch][i];
                        }

                        var numOut = _resampler[ch].Process(_buffer.NumberOfFrames);
                        if (remaining[ch] > 0)
                        {
                            if (_resampler[ch].Buffer.AvailableRead > 0)
                            {
                                var read = Math.Min(_resampler[ch].Buffer.AvailableRead, remaining[ch]);
                                var pos = bufferToFill.NumFrames - remaining[ch];
                                _resampler[ch].Buffer.Read(bufs[ch].AsSpan().Slice(pos, read), read);
                                remaining[ch] -= read;
                            }

                        }

                        if (remaining[ch] > 0)
                        {
                            finished = false;
                        }
                    }
                }
            }
        }
    }
}