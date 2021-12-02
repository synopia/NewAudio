using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Processor;
using NewAudio.Core;
using NewAudio.Device;
using Xt;
using SamplingFrequency = NewAudio.Device.SamplingFrequency;

namespace NewAudio.Nodes
{
    /// <summary>
    /// Summary
    /// </summary>
    /// <remarks>
    /// Remarks
    /// </remarks>
    public class AudioDeviceNode : AudioNode
    {
        private DeviceSelection? _selection;
        public IAudioDevice? AudioDevice;
        private SamplingFrequency[] _availableSamplingFrequencies = Array.Empty<SamplingFrequency>();

        /// <summary>
        /// Summary for Device
        /// </summary>
        /// <remarks>
        /// Remarks for Device
        /// </remarks>
        public DeviceSelection? Device
        {
            get => _selection;
            set
            {
                _selection = value;
                AudioDevice?.Dispose();
                AudioDevice = null;
                _availableSamplingFrequencies = Array.Empty<SamplingFrequency>();
               
                
                if (_selection != null)
                {
                    var name = (DeviceName)_selection.Tag;
                    AudioDevice = AudioService.OpenDevice(name.Id);
                    // _device.Open(Dsp.AudioChannels.Channels(InputChannels), Dsp.AudioChannels.Channels(OutputChannels), (int)SamplingFrequency, BufferSize);
                    _availableSamplingFrequencies =
                        AudioDevice.AvailableSampleRates.Select(s => (SamplingFrequency)s).ToArray();
                    
                }
            }
        }

        public string Name => AudioDevice?.Name ?? "";
        public string System => AudioDevice?.System.ToString() ?? "";
        public int AvailableInputChannels => AudioDevice?.AvailableInputChannels ?? 0;
        public int AvailableOutputChannels => AudioDevice?.AvailableOutputChannels ?? 0;
        public IEnumerable<string> InputChannelNames => AudioDevice?.InputChannelNames ?? Array.Empty<string>();
        public IEnumerable<string> OutputChannelNames => AudioDevice?.OutputChannelNames ?? Array.Empty<string>();
        public SamplingFrequency[] AvailableSampleFrequencies => _availableSamplingFrequencies;
        public XtSample[] AvailableSampleTypes => AudioDevice?.AvailableSampleType ?? Array.Empty<XtSample>();
        public double MinBufferSizeMs => AudioDevice?.AvailableBufferSizes.Item1 ?? 0;
        public double MaxBufferSizeMs => AudioDevice?.AvailableBufferSizes.Item2 ?? 0;
        

        private bool _disposedValue;

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
}