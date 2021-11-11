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
    }

    
    public interface IDevice : IDisposable
    {
        public string Name { get; }
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }
        AudioDataProvider AudioDataProvider { get; }

        public Task<Tuple<DeviceConfigResponse, ISourceBlock<AudioDataMessage>>> CreateInput(DeviceConfigRequest request);
        public Task<Tuple<DeviceConfigResponse, ITargetBlock<AudioDataMessage>>> CreateOutput(DeviceConfigRequest request);

        public bool Start();
        public bool Stop();

        public string DebugInfo();
    }
}