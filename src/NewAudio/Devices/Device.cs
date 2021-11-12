using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using SharedMemory;
using NewAudio.Core;

namespace NewAudio.Devices
{
    public struct DeviceConfigRequest
    {
        public AudioFormat AudioFormat { get; set; }
        public int ChannelOffset { get; set; }
        public int Channels { get; set; }
        public int Latency { get; set; }
        public int FirstChannel => ChannelOffset;
        public int LastChannel => ChannelOffset + Channels;

    }
    public struct DeviceConfigResponse
    {
        public AudioFormat AudioFormat { get; set; }
        public int ChannelOffset { get; set; }
        public int Channels { get; set; }
        public int Latency { get; set; }

        public int DriverChannels { get; set; }
        public int FrameSize { get; set; }
        
        public IEnumerable<SamplingFrequency> SupportedSamplingFrequencies;

        public int FirstChannel => ChannelOffset;
        public int LastChannel => ChannelOffset + Channels;
    }

    public class DeviceSelection 
    {
        public string Name { get; }
        public string DriverName { get; }
        
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }

        public DeviceSelection(string driverName, string name, bool isInputDevice, bool isOutputDevice)
        {
            DriverName = driverName;
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = isOutputDevice;
        }

        public override string ToString()
        {
            return $"{DriverName}: {Name}";
        }
    }
    
    public interface IDevice : IDisposable
    {
        public string Name { get; }
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }
        AudioDataProvider AudioDataProvider { get; }

        public Task<DeviceConfigResponse> CreateInput(DeviceConfigRequest request, ITargetBlock<AudioDataMessage> targetBlock);
        public Task<DeviceConfigResponse> CreateOutput(DeviceConfigRequest request, ISourceBlock<AudioDataMessage> sourceBlock);

        public bool Start();
        public bool Stop();

        public string DebugInfo();
        public DeviceConfigResponse RecordingConfig { get; }
        public DeviceConfigResponse PlayingConfig { get; }

    }
}