using System;
using System.Diagnostics;
using NewAudio.Device;
using NewAudio.Dsp;
using NewAudio.Sources;

namespace NewAudio.Device
{
    public class AudioSourcePlayer: IAudioDeviceCallback, IDisposable
    {
        public IAudioSource CurrentSource { get; }

        public float Gain
        {
            get => _gain;
            set
            {
                _gain = value;
            }
        }

        private object _readLock = new object();
        private IAudioSource _source;
        private int _sampleRate;
        private int _framesPerBlock;
        private Memory<float>[] _channels = new Memory<float>[128];
        private Memory<float>[] _outputChannels = new Memory<float>[128];
        private Memory<float>[] _inputChannels = new Memory<float>[128];
        private AudioBuffer _tempBuffer;
        private float _lastGain=1.0f;
        private float _gain = 1.0f;
        
        public AudioSourcePlayer()
        {
        }

        public void AudioDeviceCallback(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            Trace.Assert(_sampleRate>0 && _framesPerBlock>0 );
            lock (_readLock)
            {
                if (_source != null)
                {
                    var numActiveCh = 0;
                    var numInputs = 0;
                    var numOutputs = 0;
                    for (int i = 0; i < input.NumberOfChannels; i++)
                    {
                        if (!input[i].IsEmpty)
                        {
                            _inputChannels[numInputs++] = input[i];
                            if (numInputs >= _inputChannels.Length)
                            {
                                break;
                            }
                        }
                    }
                    for (int i = 0; i < output.NumberOfChannels; i++)
                    {
                        if (!output[i].IsEmpty)
                        {
                            _outputChannels[numOutputs++] = output[i];
                            if (numOutputs >= _outputChannels.Length)
                            {
                                break;
                            }
                        }
                    }

                    if (numInputs > numOutputs)
                    {
                        _tempBuffer.SetSize(numInputs-numOutputs, numFrames, false, false, true);
                        for (int i = 0; i < numOutputs; i++)
                        {
                            _channels[numActiveCh] = _outputChannels[i];
                            _inputChannels[i].Slice(0, numFrames).CopyTo(_channels[numActiveCh]);
                            numActiveCh++;
                        }

                        for (int i = numOutputs; i < numInputs; i++)
                        {
                            _channels[numActiveCh] = _tempBuffer.GetWriteChannel(i - numOutputs);
                            _inputChannels[i].Slice(0, numFrames).CopyTo(_channels[numActiveCh]);
                            numActiveCh++;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < numInputs; i++)
                        {
                            _channels[numActiveCh] = _outputChannels[i];
                            _inputChannels[i].Slice(0, numFrames).CopyTo(_channels[numActiveCh]);
                            numActiveCh++;
                        }
                        for (int i = numInputs; i < numOutputs; i++)
                        {
                            _channels[numActiveCh] = _outputChannels[i];
                            _channels[numActiveCh].Slice(0, numFrames).Span.Clear();
                            numActiveCh++;
                        }
                    }

                    var buffer = new AudioBuffer(_channels, numActiveCh, numFrames);
                    var info = new AudioSourceChannelInfo(buffer, 0, numFrames);
                    _source.GetNextAudioBlock(info);
                    for (int i = info.Buffer.NumberOfChannels; --i >= 0;)
                    {
                        // buffer.applyGainRamp(i, info.StartFrame, info.NumFrames, _lastGain, _gain);
                    }

                    _lastGain = _gain;
                }
                else
                {
                    for (int i = 0; i < output.NumberOfChannels; i++)
                    {
                        if (!output[i].IsEmpty)
                        {
                            output[i].Slice(0, numFrames).Span.Clear();
                        }
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
            if (_source != null)
            {
                _source.ReleaseResources();
            }

            _sampleRate = 0;
            _framesPerBlock = 0;
            _tempBuffer.SetSize(2, 8);
        }

        public void AudioDeviceError(string errorMessage)
        {
            throw new NotImplementedException();
        }

        public void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
            _sampleRate = sampleRate;
            _framesPerBlock = framesPerBlock;
            for (int i = 0; i < _channels.Length; i++)
            {
                _channels[i] = Memory<float>.Empty;
            }

            if (_source != null)
            {
                _source.PrepareToPlay(sampleRate, framesPerBlock);
            }
        }
        public void Dispose()
        {
            SetSource(null);
        }

        public void SetSource(IAudioSource source)
        {
            if (_source != source)
            {
                var oldSource = _source;
                if (source != null && _framesPerBlock > 0 && _sampleRate > 0)
                {
                    source.PrepareToPlay(_sampleRate, _framesPerBlock);
                }

                lock (_readLock)
                {
                    _source = source;
                }

                oldSource?.ReleaseResources();
            }
        }
    }
}