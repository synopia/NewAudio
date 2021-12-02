using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Core;
using NewAudio.Device;
using NewAudio.Dsp;
using NewAudio.Nodes;
using NewAudio.Processor;
using VL.Lang;


namespace VL.NewAudio.Nodes
{
    public class AudioSessionNode: AudioNode
    {
        private AudioGraph _graph = new();
        private AudioDeviceNode? _inputDevice;
        private AudioDeviceNode? _outputDevice;
        private bool _enabled;
        private AudioSession? _session;
        private CallbackHandler _callbackHandler;
        private AudioChannels _inputChannels;
        private AudioChannels _outputChannels;

        private string[] _inputChannelNames=Array.Empty<string>();
        private string[] _outputChannelNames=Array.Empty<string>();
        private RenderingProgram? _program;
        private AudioLink? _input;
        
        public AudioLink? Input
        {
            get => _input;
            set
            {
                if (_input == value)
                {
                    return;
                }

                _input?.Disconnect();

                _input = value;

                if (_input != null )
                {
                    _input.Connect(_graph);
                }

                UpdateGraph();
            }
        }


        public IEnumerable<string> InputChannels
        {
            get => _inputChannelNames;
            set
            {
                _inputChannelNames = value.ToArray();
                var inputDevice = _inputDevice?.AudioDevice;
                if (inputDevice != null)
                {
                    _inputChannels = UpdateChannels(inputDevice.InputChannelNames, _inputChannelNames);
                }
            }
        }
        
        public IEnumerable<string> OutputChannels
        {
            get => _outputChannelNames;
            set
            {
                _outputChannelNames = value.ToArray();
                var outputDevice = _outputDevice?.AudioDevice;
                if (outputDevice != null)
                {
                    _outputChannels = UpdateChannels(outputDevice.OutputChannelNames, _outputChannelNames);
                }
            }
        }

        public SamplingFrequency SamplingFrequency { get; set; } = SamplingFrequency.Hz44100;
        public double BufferSize { get; set; } = 0;
        
        
        public AudioDeviceNode? InputDevice
        {
            get => _inputDevice;
            set
            {
                _inputDevice = value;
                Update();
            }
        }

        public AudioDeviceNode? OutputDevice
        {
            get => _outputDevice;
            set
            {
                _outputDevice = value;
                Update();
            }
        }

        public override bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Update();
            }
        }

        private AudioChannels UpdateChannels(string[] allChannels, string[] selectedChannels)
        {
            int channel = 0;
            ulong channelMask = 0;
            foreach (var channelName in allChannels)
            {
                if (selectedChannels.Contains(channelName))
                {
                    channelMask |= (ulong)1 << channel;
                }

                channel++;
            }
            return AudioChannels.FromMask(channelMask);
        }
        
        private void Update()
        {
            
            if ( _outputDevice?.AudioDevice != null )
            {
                var input = _inputDevice?.AudioDevice;
                var output = _outputDevice.AudioDevice;
                
                if (_outputChannels.Count > 0)
                {
                    if (input != null && output.Id != input.Id)
                    {
                        input.Open(_inputChannels, AudioChannels.Disabled, (int)SamplingFrequency, BufferSize);
                        output.Open(AudioChannels.Disabled, _outputChannels, (int)SamplingFrequency, BufferSize);
                    }
                    else
                    {
                        output.Open(_inputChannels, _outputChannels, (int)SamplingFrequency, BufferSize);
                    }

                    _session = new AudioSession(input, output);
                    _callbackHandler
                    _session.Start(_callbackHandler);
                }
            }
        }


        public void UpdateGraph()
        {
            _program = new RenderingProgram();
            var builder = new RenderingBuilder(_graph, _program);
        }
    }
}