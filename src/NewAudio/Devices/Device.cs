using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Internal;

namespace NewAudio.Devices
{
    /*
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
    */

    public class DeviceParams : AudioParams
    {
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> DesiredLatency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> Channels;

        public int FirstChannel => ChannelOffset.Value;
        public int LastChannel => FirstChannel + Channels.Value;

        public AudioFormat AudioFormat => new AudioFormat((int)SamplingFrequency.Value, 512, Channels.Value);
    }
    public class ActualDeviceParams : AudioParams
    {
        public AudioParam<bool> IsRecordingDevice;
        public AudioParam<bool> IsPlayingDevice;
        public AudioParam<bool> Active;
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> Latency;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> Channels;
        public AudioParam<WaveFormat> WaveFormat;
        public int FirstChannel => ChannelOffset.Value;
        public int LastChannel => FirstChannel + Channels.Value;

        public AudioFormat AudioFormat => new AudioFormat((int)SamplingFrequency.Value, 512, Channels.Value);
    }

    public interface IVirtualDevice: IDisposable
    {
        IDevice Device { get; }
        string Name { get; }

        DeviceParams Params { get; }
        ActualDeviceParams ActualParams { get; }
        
        bool IsPlaying { get; }
        bool IsRecording { get; }

        void Update();
        void Post(AudioDataMessage msg);
    }
    
    public class DeviceSelection
    {
        public string Name { get; }
        public string DriverName { get; }
        public string NamePrefix { get; }

        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }

        public DeviceSelection(string driverName, string namePrefix, string name, bool isInputDevice, bool isOutputDevice)
        {
            DriverName = driverName;
            Name = name;
            NamePrefix = namePrefix;
            IsInputDevice = isInputDevice;
            IsOutputDevice = isOutputDevice;
        }

        public override string ToString()
        {
            return $"{NamePrefix}: {Name}";
        }
    }

    public interface IDevice : IDisposable
    {
        public string Name { get; }
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }
        AudioDataProvider AudioDataProvider { get; }

        IMixBuffer GetMixBuffer();
        void OnDataReceived(byte[] buffer);

        public ActualDeviceParams Add(VirtualInput input);
        public void Remove(VirtualInput input);
        public ActualDeviceParams Add(VirtualOutput output);
        public void Remove(VirtualOutput output);

        public void Update();
        
        public ActualDeviceParams RecordingParams { get; }
        public ActualDeviceParams PlayingParams { get; }
        public string DebugInfo();
    }
}