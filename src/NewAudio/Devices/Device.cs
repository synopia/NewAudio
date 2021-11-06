using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using SharedMemory;
using NewAudio.Core;

namespace NewAudio.Devices
{
    public class DeviceConfig
    {
        public CircularBuffer Buffer { get; set; }
        public WaveFormat WaveFormat { get; set; }
        public int ChannelOffset { get; set; }
        public int Channels { get; set; }
        public int Latency { get; set; }
    }
    public class DeviceConfigRequest
    {
        public DeviceConfig Recording { get; set; }
        public DeviceConfig Playing { get; set; }
        public bool IsPlaying => Playing != null;
        public bool IsRecording => Recording != null;
    }
    public struct DeviceConfigResponse
    {
        public WaveFormat PlayingWaveFormat;
        public WaveFormat RecordingWaveFormat;
        public int DriverRecordingChannels;
        public int DriverPlayingChannels;
        public int RecordingChannels;
        public int PlayingChannels;
        public int FrameSize;
        public int Latency;
        public IEnumerable<SamplingFrequency> SupportedSamplingFrequencies;
    }

    
    public interface IDevice : IDisposable
    {
        public string Name { get; }
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }
        AudioDataProvider AudioDataProvider { get; }

        public Task<DeviceConfigResponse> Create(DeviceConfigRequest request);

        public Task<bool> Free();
        public bool Start();
        public bool Stop();
    }

    public abstract class BaseDevice : IDevice
    {
        public AudioDataProvider AudioDataProvider { get; protected set; }

        public string Name { get; protected set; }

        public bool IsInputDevice { get; protected set; }

        public bool IsOutputDevice { get; protected set; }
        protected CancellationTokenSource CancellationTokenSource;
        
        public abstract Task<DeviceConfigResponse> Create(DeviceConfigRequest config);
        public abstract Task<bool> Free();
        public abstract bool Start();
        public abstract bool Stop();

        private bool _disposedValue;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            AudioService.Instance.Logger.Information("Dispose called for Device {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    
                }

                _disposedValue = true;
            }
        }
    }
}