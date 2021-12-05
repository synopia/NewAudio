using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Processor;
using NewAudio.Core;
using NewAudio.Device;
using NewAudio.Dsp;
using VL.Lang;
using Xt;
using SamplingFrequency = NewAudio.Device.SamplingFrequency;

namespace NewAudio.Nodes
{
    /// <summary>
    /// An audio device, to be used as input or output by a AudioSession. Available are ASIO, Wasapi and DirectSound devices.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class AudioDeviceNode : AudioNode
    {
        private SamplingFrequency[] _availableSamplingFrequencies = Array.Empty<SamplingFrequency>();
        public IAudioDevice? AudioDevice { get; set; }

        /// <summary>
        /// The actual device
        /// </summary>
        /// <remarks>
        /// Select nothing (null) will disable the device.  
        /// </remarks>
        public DeviceSelection? Device { get; set; }

        public string Name => AudioDevice?.Name ?? "";
        public string System => AudioDevice?.System.ToString() ?? "";
        /// <summary>
        /// Number of input channels available
        /// </summary>
        public int AvailableInputChannels => AudioDevice?.NumAvailableInputChannels ?? 0;
        /// <summary>
        /// Number of output channels available
        /// </summary>
        public int AvailableOutputChannels => AudioDevice?.NumAvailableOutputChannels ?? 0;
        /// <summary>
        /// Sequence of input channel names. Should be used by an AudioSession to select actual inputs.  
        /// </summary>
        public IEnumerable<string> InputChannelNames => AudioDevice?.InputChannelNames ?? Array.Empty<string>();
        /// <summary>
        /// Sequence of output channel names. Should be used by an AudioSession to select actual outputs.  
        /// </summary>
        public IEnumerable<string> OutputChannelNames => AudioDevice?.OutputChannelNames ?? Array.Empty<string>();
        /// <summary>
        /// Sequence of available sampling frequencies. 
        /// </summary>
        public IEnumerable<SamplingFrequency> AvailableSampleFrequencies => _availableSamplingFrequencies;
        /// <summary>
        /// Sequence of available sample types. Internally, only Float32 is used. Other formats are converted automatically.
        /// </summary>
        public IEnumerable<XtSample> AvailableSampleTypes => AudioDevice?.AvailableSampleTypes ?? Array.Empty<XtSample>();
        /// <summary>
        /// Minimum buffer size in milliseconds. 
        /// </summary>
        public double MinBufferSizeMs => AudioDevice?.AvailableBufferSizes.Item1 ?? 0;
        /// <summary>
        /// Maximum buffer size in milliseconds. 
        /// </summary>
        public double MaxBufferSizeMs => AudioDevice?.AvailableBufferSizes.Item2 ?? 0;

        public override bool IsEnabled => IsEnable && AudioDevice!=null;

        private bool _disposedValue;

        public override Message? Update(ulong mask)
        {
            AudioDevice?.Dispose();
            AudioDevice = null;
            _availableSamplingFrequencies = Array.Empty<SamplingFrequency>();

            if (!IsEnable)
            {
                return null;
            }
            if (Device == null)
            {
                return new Message(MessageSeverity.Error, "No device selected");
            }

            var name = (DeviceName)Device.Tag;
            AudioDevice = AudioService.OpenDevice(name.Id);
            _availableSamplingFrequencies = AudioDevice.AvailableSampleRates.Select(s => (SamplingFrequency)s).ToArray();

            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    AudioDevice?.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
    
    public class AudioStreamConfigNode: AudioNode
    {
        public IAudioDevice? AudioDevice { get; set; }
        
        /// <summary>
        /// The sampling frequency to use. If nothing is selected or selected frequency is not available, the smallest above 44kHz is chosen.
        /// </summary>
        public SamplingFrequency SamplingFrequency { get; set; } = SamplingFrequency.Hz44100;
        /// <summary>
        /// The buffer size to use. If below min or above max, the default buffer size is chosen.
        /// </summary>
        public double BufferSize { get; set; } = 0;
        
        /// <summary>
        /// Input channels to use. You get the available channel names from the AudioDevice node.  
        /// </summary>
        public IEnumerable<string> InputChannels { get; set; }

        /// <summary>
        /// Output channels to use. You get the available channel names from the AudioDevice node.  
        /// </summary>
        public IEnumerable<string> OutputChannels { get; set; }

        public override bool IsEnabled => IsEnable && (Config?.IsValid ?? false);

        public AudioStreamConfig? Config { get; private set; }

        public AudioStreamConfigNode()
        {
            OutputChannels = Array.Empty<string>();
            InputChannels = Array.Empty<string>();
        }

        public override Message? Update(ulong mask)
        {
            if (AudioDevice == null)
            {
                Config = null;
                return null;
            }

            bool created = false;

            if (HasChanged(nameof(AudioDevice), mask) || Config == null)
            {
                Config = new AudioStreamConfig(AudioDevice);
                created = true;
            }

            AudioChannels inputChannels = Config.ActiveInputChannels;
            AudioChannels outputChannels = Config.ActiveOutputChannels;
            
            if (created || HasChanged(nameof(OutputChannels), mask))
            {
                outputChannels = AudioChannels.FromNames(AudioDevice, false, OutputChannels.ToArray());
            }

            if (created || HasChanged(nameof(InputChannels), mask))
            {
                inputChannels = AudioChannels.FromNames(AudioDevice, true, InputChannels.ToArray());
            }

            Config.Config(inputChannels, outputChannels, (int)SamplingFrequency, BufferSize);
            Config.IsEnabled = IsEnable;
            
            return null;
        }
    }
}