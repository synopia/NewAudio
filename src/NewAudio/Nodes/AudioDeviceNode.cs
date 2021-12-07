using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VL.NewAudio.Processor;
using VL.NewAudio.Core;
using VL.NewAudio.Device;
using VL.Lang;
using VL.Lib.Basics.Resources;
using Xt;

namespace VL.NewAudio.Nodes
{
    /// <summary>
    /// Audio device, to be used with an AudioStream. Available are ASIO, Wasapi and DirectSound devices.
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    public class AudioDeviceNode : AudioNode
    {
        private SamplingFrequency[] _availableSamplingFrequencies = Array.Empty<SamplingFrequency>();
        private DeviceSelection? _deviceSelection;
        private IResourceHandle<IAudioDevice>? _audioDevice;
        public IAudioDevice? AudioDevice => _audioDevice?.Resource;

        /// <summary>
        /// The actual device
        /// </summary>
        /// <remarks>
        /// Select nothing (null) will disable the device.  
        /// </remarks>
        public DeviceSelection? Device
        {
            get=>_deviceSelection;
            set
            {
                _audioDevice?.Dispose();
                _deviceSelection = value;
                if (_deviceSelection != null)
                {
                    var name = (DeviceName)_deviceSelection.Tag;
                    _audioDevice = AudioService.OpenDevice(name.Id);
                    if (AudioDevice != null)
                    {
                        _availableSamplingFrequencies =
                            AudioDevice.AvailableSampleRates.Select(s => (SamplingFrequency)s).ToArray();
                    }
                }
                else
                {
                    _audioDevice = null;
                    _availableSamplingFrequencies = Array.Empty<SamplingFrequency>();
                }
            }
        }

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

        private bool _disposedValue;


        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _audioDevice?.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}