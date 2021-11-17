using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Internal;

namespace NewAudio.Devices
{
   public class DeviceConfigRequest
    {
        public SamplingFrequency SamplingFrequency;
        public int DesiredLatency;
        public int ChannelOffset;
        public int Channels;
        public int FramesPerBlock;

        public int FirstChannel => ChannelOffset;
        public int LastChannel => FirstChannel + Channels;
    }
    public class DeviceConfig 
    {
        public bool Active;
        public SamplingFrequency SamplingFrequency;
        public int FramesPerBlock;
        public int Latency;
        public int ChannelOffset;
        public int Channels;
        public WaveFormat WaveFormat;
        public int FirstChannel => ChannelOffset;
        public int LastChannel => FirstChannel + Channels;
    }

    public interface IVirtualDevice: IDisposable
    {
        IAudioClient AudioClient { get; }
        string Name { get; }

        DeviceConfig Config { get; }
        
        bool IsPlaying { get; }
        bool IsRecording { get; }

        void Post(AudioDataMessage msg);
        void Start();
        void Stop();
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

    public interface IAudioClient : IDisposable
    {
        public string Name { get; }
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }

        public void Add(VirtualInput input);
        public void Remove(VirtualInput input);
        public void Add(VirtualOutput output);
        public void Remove(VirtualOutput output);

        public void Update();
        
        public DeviceConfig RecordingParams { get; }
        public DeviceConfig PlayingParams { get; }
        public string DebugInfo();
        void Pause(IVirtualDevice device);
        void UnPause(IVirtualDevice device);
    }
}