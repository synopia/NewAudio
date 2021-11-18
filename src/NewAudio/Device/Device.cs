using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Dsp;
using NewAudio.Internal;
using Serilog;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class DeviceConfig : AudioParams
    {
        public AudioParam<int> SampleRate;
        public AudioParam<int> FramesPerBlock;
    }

    public interface IDevice : IDisposable
    {
        public OutputDeviceBlock Output { get; set; }
        int NumberOfInputChannels { get; }
        int NumberOfOutputChannels { get; }
        int SampleRate { get; }
        int FramesPerBlock { get; }
        string Name { get; }
        void Initialize();
        void Uninitialize();
        void EnableProcessing();
        void DisableProcessing();
        void Update();
    }
    
    public abstract class BaseDevice : IDevice
    {
        protected int _sampleRate;
        protected int _framesPerBlock;
        private readonly IResourceHandle<AudioService> _audioService;
        protected ILogger Logger;

        public abstract string Name { get; }
        public abstract int NumberOfInputChannels { get; }
        public abstract int NumberOfOutputChannels { get; }

        public int SampleRate
        {
            get
            {
                if (_sampleRate == 0)
                {
                    _sampleRate = Config.SampleRate.Value; 
                }

                return _sampleRate;
            }
        }

        public int FramesPerBlock
        {
            get
            {
                if (_framesPerBlock == 0)
                {
                    _framesPerBlock = Config.FramesPerBlock.Value;
                }

                return _framesPerBlock;
            }
        }

        public DeviceConfig Config { get; }
        public OutputDeviceBlock Output { get; set; }


        protected BaseDevice()
        {
            _audioService = Factory.GetAudioService();
            Config = AudioParams.Create<DeviceConfig>();
        }
        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public void UpdateFormat(int sampleRate, int framesPerBlock)
        {
            Config.SampleRate.Value = sampleRate;
            Config.FramesPerBlock.Value = framesPerBlock;

            if (Config.HasChanged)
            {
                _sampleRate = 0;
                _framesPerBlock = 0;
            }
        }

        public abstract void Initialize();

        public abstract void Uninitialize();
        public abstract void EnableProcessing();
        public abstract void DisableProcessing();

        public virtual void Update()
        {
            
        }

        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for Device {This} ({Disposing})", Name, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _audioService?.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
    
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
        public AudioParam<int> ConnectedDevices;
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
    */

}