using System;
using System.Collections.Generic;
using System.Linq;
using VL.NewAudio.Dsp;
using VL.NewAudio.Core;

namespace VL.NewAudio.Nodes
{
    /// <summary>
    /// Open and configures an audio device. 
    /// </summary>
    public class AudioStreamNode: AudioNode
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

        public override bool IsEnabled => Config?.IsValid ?? false;

        public AudioStreamConfig? Config
        {
            get
            {
                if (AudioDevice != null)
                {
                    return new AudioStreamConfig(AudioDevice)
                    {
                        ActiveOutputChannels = AudioChannels.FromNames(AudioDevice, false, OutputChannels.ToArray()),
                        ActiveInputChannels = AudioChannels.FromNames(AudioDevice, true, InputChannels.ToArray()),
                        SampleRate = (int)SamplingFrequency,
                        BufferSize = BufferSize,
                        IsEnabled = IsEnable,
                        Interleaved = AudioDevice?.Caps?.Interleaved ?? true
                    }.Match();
                }

                return null;
            }
        }

        public AudioStreamNode()
        {
            OutputChannels = Array.Empty<string>();
            InputChannels = Array.Empty<string>();
        }
    }
}