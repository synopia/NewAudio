using System;
using System.Diagnostics;
using System.Threading;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;

namespace VL.NewAudio.Sources
{
    public class AudioSourcePlayer : IAudioCallback, IDisposable
    {
        public float Gain { get; set; } = 1.0f;
        
        public AudioParam<float> GGain { get; set; }

        private IAudioSource? _source;

        public IAudioSource? Source
        {
            get => _source;
            set
            {
                if (_source != value)
                {
                    var oldSource = _source;

                    if (value != null && _framesPerBlock > 0 && _sampleRate > 0)
                    {
                        value.PrepareToPlay(_sampleRate, _framesPerBlock);
                    }

                    _source = value;

                    oldSource?.ReleaseResources();
                }
            }
        }

        private int _sampleRate;
        private int _framesPerBlock;
        private readonly AudioBuffer _tempBuffer = new();
        private float _lastGain = 1.0f;
        private bool _disposedValue;

        public AudioSourcePlayer()
        {
        }

        public void OnAudio(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            Trace.Assert(_sampleRate > 0 && _framesPerBlock > 0);
            using var s = new ScopedMeasure("AudioSourcePlayer.OnAudio");
                var totalInputs = input?.NumberOfChannels ?? 0;
                var totalOutputs = output.NumberOfChannels;
                _tempBuffer.Merge(input, output, totalInputs, totalOutputs);

                if (Source != null)
                {
                    var info = new AudioBufferToFill(_tempBuffer, 0, numFrames);
                    Source.FillNextBuffer(info);

                    for (var i = info.Buffer.NumberOfChannels; --i >= 0;)
                    {
                        // buffer.applyGainRamp(i, info.StartFrame, info.NumFrames, _lastGain, _gain);
                    }

                    _lastGain = Gain;
                }
                else
                {
                    for (var i = 0; i < output.NumberOfChannels; i++)
                    {
                        output.ZeroChannel(i);
                    }
                }
        }

        public void OnAudioWillStart(IAudioSession session)
        {
            _sampleRate = session.CurrentSampleRate;
            _framesPerBlock = session.CurrentFramesPerBlock;

            Source?.PrepareToPlay(_sampleRate, _framesPerBlock);
        }


        public void OnAudioStopped()
        {
            Source?.ReleaseResources();

            _sampleRate = 0;
            _framesPerBlock = 0;
            _tempBuffer.SetSize(2, 8);
        }

        public void OnAudioError(string errorMessage)
        {
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
                    Source = null;
                }

                _disposedValue = true;
            }
        }
    }
}