using System;
using System.Collections.Generic;
using System.Linq;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Sources
{
    public class ChannelRouterSource : AudioSourceBase
    {
        private int[] _inputMap = Array.Empty<int>();
        private int[] _outputMap = Array.Empty<int>();
        private AudioBuffer _buffer = new();
        private int _requiredChannels = 2;
        public IAudioSource? Source { get; set; }

        public IEnumerable<int> InputMap
        {
            get => _inputMap;
            set { _inputMap = value.ToArray(); }
        }

        public IEnumerable<int> OutputMap
        {
            get => _outputMap;
            set { _outputMap = value.ToArray(); }
        }

        public int NumberOfChannelsToProduce
        {
            get => _requiredChannels;
            set { _requiredChannels = value; }
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
            if (index >= 0 && index < _inputMap.Length)
            {
                return _inputMap[index];
            }

            return -1;
        }

        public int GetRemappedOutput(int index)
        {
            if (index >= 0 && index < _outputMap.Length)
            {
                return _outputMap[index];
            }

            return -1;
        }

        public override void FillNextBuffer(AudioBufferToFill buffer)
        {
            using var s = new ScopedMeasure("ChannelRouterSource.GetNextAudioBlock");
            _buffer.SetSize(_requiredChannels, buffer.NumFrames, false, false, true);
            var numChannels = buffer.Buffer.NumberOfChannels;
            var numFrames = buffer.NumFrames;

            for (var i = 0; i < _buffer.NumberOfChannels; i++)
            {
                var remapped = GetRemappedInput(i);
                if (remapped >= 0 && remapped < numChannels)
                {
                    buffer.Buffer.GetReadChannel(remapped).Offset(buffer.StartFrame)
                        .CopyTo(_buffer.GetWriteChannel(i), numFrames);
                }
                else
                {
                    _buffer.ZeroChannel(i);
                }
            }

            var remappedInfo = new AudioBufferToFill(_buffer, 0, numFrames);

            Source?.FillNextBuffer(remappedInfo);

            buffer.ClearActiveBuffer();
            for (var i = 0; i < _requiredChannels; i++)
            {
                var remapped = GetRemappedOutput(i);
                if (remapped >= 0 && remapped < numChannels)
                {
                    buffer.Buffer[remapped].Offset(buffer.StartFrame)
                        .Add(_buffer[i], numFrames);
                }
            }
        }
    }
}