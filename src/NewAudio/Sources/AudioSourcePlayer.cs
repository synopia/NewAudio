using System;
using System.Diagnostics;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Sources
{
    public class AudioSourcePlayer: IAudioDeviceCallback, IDisposable
    {
        public float Gain { get; set; } = 1.0f;

        private readonly object _readLock = new();
        private IAudioSource? _source;
        public IAudioSource? Source
        {
            get=>_source;
            set
            {
                if (_source != value)
                {
                    var oldSource = _source;
                
                    if (value != null && _framesPerBlock > 0 && _sampleRate > 0)
                    {
                        value.PrepareToPlay(_sampleRate, _framesPerBlock);
                    }

                    lock (_readLock)
                    {
                        _source = value;
                    }

                    oldSource?.ReleaseResources();
                }
            }
        }

        private int _sampleRate;
        private int _framesPerBlock;
        private readonly AudioBuffer _tempBuffer = new();
        private float _lastGain=1.0f;
        private bool _disposedValue;

        public AudioSourcePlayer()
        {
        }

        public void AudioDeviceCallback(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            Trace.Assert(_sampleRate>0 && _framesPerBlock>0 );
            lock (_readLock)
            {
                var totalInputs = input?.NumberOfChannels ?? 0;
                var totalOutputs = output.NumberOfChannels;
                _tempBuffer.Merge(input, output, totalInputs, totalOutputs);

                if (Source != null)
                {

                    var info = new AudioSourceChannelInfo(_tempBuffer, 0, numFrames);
                    Source.GetNextAudioBlock(info);
                    
                    for (int i = info.Buffer.NumberOfChannels; --i >= 0;)
                    {
                        // buffer.applyGainRamp(i, info.StartFrame, info.NumFrames, _lastGain, _gain);
                    }

                    _lastGain = Gain;
                }
                else
                {
                    for (int i = 0; i < output.NumberOfChannels; i++)
                    {
                        output.ZeroChannel(i);
                    }
                }
            }
        }

        public void AudioDeviceAboutToStart(IAudioSession session)
        {
            PrepareToPlay(session.CurrentSampleRate, session.CurrentFramesPerBlock);
        }
        

        public void AudioDeviceStopped()
        {
            Source?.ReleaseResources();

            _sampleRate = 0;
            _framesPerBlock = 0;
            _tempBuffer.SetSize(2, 8);
        }

        public void AudioDeviceError(string errorMessage)
        {
            
        }

        public void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
            _sampleRate = sampleRate;
            _framesPerBlock = framesPerBlock;

            Source?.PrepareToPlay(sampleRate, framesPerBlock);
        }
        
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Source=null;
                }

                _disposedValue = true;
            }
        }

    }
}