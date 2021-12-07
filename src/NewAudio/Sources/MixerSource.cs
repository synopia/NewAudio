using System;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Device
{
    public class MixerSource: AudioSourceNode
    {
        private int _currentSampleRate;
        private int _currentBufferSize;
        private object _lock = new();
        private AudioBuffer _tempBuffer;
        private AudioConnection[] _connections = Array.Empty<AudioConnection>();
        
        public AudioConnection[] Inputs
        {
            get => _connections;
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
                    foreach (var connection in value)
                    {
                        if (Array.IndexOf(_connections, connection) == -1)
                        {
                            connection.Source.PrepareToPlay(sampleRate, bufferSize);
                        }
                    }
                }
                
                foreach (var connection in _connections)
                {
                    if (Array.IndexOf(value, connection) == -1)
                    {
                        connection.Source.ReleaseResources();
                    }
                }

                lock (_lock)
                {
                    _connections = value;
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
                foreach (var input in Inputs)
                {
                    input.Source.PrepareToPlay(sampleRate, framesPerBlockExpected);
                }
            }
        }

        public override void ReleaseResources()
        {
            lock (_lock)
            {
                foreach (var input in Inputs)
                {
                    input.Source.ReleaseResources();
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
                if (Inputs.Length > 0)
                {
                    Inputs[0].Source.GetNextAudioBlock(bufferToFill);
                    if (Inputs.Length > 1)
                    {
                        var numFrames = bufferToFill.NumFrames;
                        _tempBuffer.SetSize(Math.Max(1,bufferToFill.Buffer.NumberOfChannels), numFrames);
                        var buf = new AudioSourceChannelInfo(_tempBuffer, 0, numFrames);
                        for (int i = 1; i < Inputs.Length; i++)
                        {
                            Inputs[i].Source.GetNextAudioBlock(buf);
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