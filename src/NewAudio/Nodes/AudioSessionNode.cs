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
        private AudioDeviceNode? _inputDeviceNode;
        private AudioDeviceNode? _outputDeviceNode;
        private bool _enabled;
        private IAudioControl _control;
        private AudioChannels _inputChannels;
        private AudioChannels _outputChannels;

        private string[] _inputChannelNames=Array.Empty<string>();
        private string[] _outputChannelNames=Array.Empty<string>();
        private RenderingProgram? _program;
        private AudioLink? _input;
        private AudioProcessorPlayer _graphPlayer;
        private AudioGraphIOProcessor _graphInput;
        private AudioGraphIOProcessor _graphOutput;
        private AudioGraph.Node _graphInputNode;
        private AudioGraph.Node _graphOutputNode;

        public AudioSessionNode()
        {
            _control = new XtAudioControl(AudioService);
            _graphInput = new AudioGraphIOProcessor(false);
            _graphOutput = new AudioGraphIOProcessor(true);
            _graphInputNode = _graph.AddNode(_graphInput)!;
            _graphOutputNode = _graph.AddNode(_graphOutput)!;
            
            _graphPlayer = new AudioProcessorPlayer();
            _control.AddAudioCallback(_graphPlayer);
            _graphPlayer.SetProcessor(_graph);
        }
        
        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _control.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

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
                    _input.Connect(_graph, _graphOutputNode);
                }

                // UpdateGraph();
            }
        }


        public IEnumerable<string> InputChannels
        {
            get => _inputChannelNames;
            set
            {
                _inputChannelNames = value.ToArray();
                var inputDevice = _inputDeviceNode?.AudioDevice;
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
                var outputDevice = _outputDeviceNode?.AudioDevice;
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
            get => _inputDeviceNode;
            set
            {
                _inputDeviceNode = value;
                Update();
            }
        }

        public AudioDeviceNode? OutputDevice
        {
            get => _outputDeviceNode;
            set
            {
                _outputDeviceNode = value;
                Update();
            }
        }

        public override bool Enable
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Update();
            }
        }

        public override bool Enabled => _control.IsRunning;

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
            if (Enable)
            {
                var outputDevice = _outputDeviceNode?.AudioDevice;
                if (outputDevice != null)
                {
                    try
                    {
                        var session = _control.Open(_inputDeviceNode?.AudioDevice, outputDevice, _inputChannels, _outputChannels,
                            (int)SamplingFrequency,
                            BufferSize);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        
                    }
                }
            }
            else
            {
                _control.Close();
            }
        }


        public void UpdateGraph()
        {

            _program = new RenderingProgram();
            var builder = new RenderingBuilder(_graph, _program);
        }

    }
}