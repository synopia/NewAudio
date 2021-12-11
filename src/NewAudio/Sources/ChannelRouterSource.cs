using System;
using System.Collections.Generic;
using System.Linq;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Sources
{
    public class ChannelRouterSource: AudioSourceNode
    {
        private int[] _inputMap = Array.Empty<int>();
        private int[] _outputMap = Array.Empty<int>();
        private AudioBuffer _buffer = new ();
        private object _lock = new();
        private int _requiredChannels = 2;
        public IAudioSource? Source { get; set; }

        public IEnumerable<int> InputMap
        {
            get => _inputMap;
            set
            {
                lock (_lock)
                {
                    _inputMap = value.ToArray();
                }
            }
        }

        public IEnumerable<int> OutputMap
        {
            get => _outputMap;
            set
            {
                lock (_lock)
                {
                    _outputMap = value.ToArray();
                }
            }
        }
        
        public int NumberOfChannelsToProduce
        {
            get => _requiredChannels;
            set
            {
                lock (_lock)
                {
                    _requiredChannels = value;
                }
            }
        }

        public ChannelRouterSource()
        {
            
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            Source?.PrepareToPlay(sampleRate, framesPerBlockExpected);
        }

        public override void ReleaseResources()
        {
            
        }

        public int GetRemappedInput(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _inputMap.Length)
                {
                    return _inputMap[index];
                }

                return -1;
                    
            }
        }
        
        public int GetRemappedOutput(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _outputMap.Length)
                {
                    return _outputMap[index];
                }

                return -1;
                    
            }
        }
        
        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            lock (_lock)
            {
                _buffer.SetSize(_requiredChannels, bufferToFill.NumFrames, false, false, true);
                int numChannels = bufferToFill.Buffer.NumberOfChannels;
                var numFrames = bufferToFill.NumFrames;

                for (int i = 0; i < _buffer.NumberOfChannels; i++)
                {
                    var remapped = GetRemappedInput(i);
                    if (remapped >= 0 && remapped<numChannels )
                    {
                        bufferToFill.Buffer.GetReadChannel(remapped).Slice(bufferToFill.StartFrame, numFrames)
                            .CopyTo(_buffer.GetWriteChannel(i).Span);
                    }
                    else
                    {
                        _buffer.ZeroChannel(i);
                    }
                }

                var remappedInfo = new AudioSourceChannelInfo(_buffer, 0, numFrames); 
                
                Source?.GetNextAudioBlock(remappedInfo);
                
                bufferToFill.ClearActiveBuffer();
                for (int i = 0; i < _requiredChannels; i++)
                {
                    var remapped = GetRemappedOutput(i);
                    if (remapped >= 0 && remapped < numChannels)
                    {
                        bufferToFill.Buffer[remapped].Slice(bufferToFill.StartFrame).Span.Add(_buffer[i].Slice(0).Span, numFrames);
                    }
                }
            }
        }
    }
}