using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VL.Lib;
using VL.Lib.Collections;

namespace NewAudio
{
    [Serializable]
    public class WaveOutputDevice : DynamicEnumBase<WaveOutputDevice, WaveOutputDeviceDefinition>
    {
        public WaveOutputDevice(string value) : base(value)
        {
        }

        public static WaveOutputDevice CreateDefault() => CreateDefaultBase("No audio output device found");
    }

    public interface IWaveOutputFactory
    {
        IWavePlayer Create(int latency);
    }

    public class WaveOutFactory : IWaveOutputFactory
    {
        private int deviceId;

        public WaveOutFactory(int deviceId)
        {
            this.deviceId = deviceId;
        }

        public IWavePlayer Create(int latency)
        {
            var waveOut = new WaveOutEvent {DeviceNumber = deviceId, DesiredLatency = latency};
            return waveOut;
        }
    }

    public class DirectSoundOutFactory : IWaveOutputFactory
    {
        private Guid guid;

        public DirectSoundOutFactory(Guid guid)
        {
            this.guid = guid;
        }

        public IWavePlayer Create(int latency)
        {
            var directSoundOut = new DirectSoundOut(guid, latency);
            return directSoundOut;
        }
    }

    public class WasapiOutFactory : IWaveOutputFactory
    {
        // private readonly Logger _logger = LogFactory.Instance.Create("WasapiOutFactory");
        private string deviceId;

        public WasapiOutFactory(string deviceId)
        {
            this.deviceId = deviceId;
        }

        public IWavePlayer Create(int latency)
        {
            var wasapi = new MMDeviceEnumerator().GetDevice(deviceId);
            var wavePlayer = new WasapiOut(wasapi, AudioClientShareMode.Shared, true, latency);
            return wavePlayer;
        }
    }

    public class AsioOutFactory : IWaveOutputFactory
    {
        private string driverName;

        public AsioOutFactory(string driverName)
        {
            this.driverName = driverName;
        }

        public IWavePlayer Create(int latency)
        {
            var asioOut = new AsioOut(driverName);
            // _logger.Info($"DriverInputChannelCount {asioOut.DriverInputChannelCount}");
            // _logger.Info($"DriverOutputChannelCount {asioOut.DriverOutputChannelCount}");
            // _logger.Info($"PlaybackLatency {asioOut.PlaybackLatency}");
            // _logger.Info($"NumberOfInputChannels {asioOut.NumberOfInputChannels}");
            // _logger.Info($"NumberOfOutputChannels {asioOut.NumberOfOutputChannels}");
            // _logger.Info($"ChannelOffset {asioOut.ChannelOffset}");
            // _logger.Info($"InputChannelOffset {asioOut.InputChannelOffset}");
            // _logger.Info($"FramesPerBuffer {asioOut.FramesPerBuffer}");
            // _logger.Info($"{asioOut.IsSampleRateSupported(48000)}");
            // _logger.Info($"{asioOut.IsSampleRateSupported(44100)}");
//            AudioEngine.Log($"{asioOut.}");
//            AudioEngine.Log($"{asioOut.}");

            return asioOut;
        }
    }

    public class NullAudioPlayer : IWavePlayer
    {
        public void Dispose()
        {
        }

        public void Play()
        {
        }

        public void Stop()
        {
        }

        public void Pause()
        {
        }

        public void Init(IWaveProvider waveProvider)
        {
        }

        public float Volume { get; set; }
        public PlaybackState PlaybackState { get; }
        public event EventHandler<StoppedEventArgs> PlaybackStopped;
    }
    public class NullAudioFactory: IWaveOutputFactory
    {
        public IWavePlayer Create(int latency)
        {
            return new NullAudioPlayer();
        }
    }
    public class WaveOutputDeviceDefinition : DynamicEnumDefinitionBase<WaveOutputDeviceDefinition>
    {
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            Dictionary<string, object> devices = new Dictionary<string, object>()
            {
                ["NullAudio"] = new NullAudioFactory()
            };

            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                var name = caps.ProductName;
                devices[$"WaveOut: {name}"] = new WaveOutFactory(i);
            }

            foreach (var device in DirectSoundOut.Devices)
            {
                var name = device.Description;
                devices[$"DS: {name}"] = new DirectSoundOutFactory(device.Guid);
            }

            var enumerator = new MMDeviceEnumerator();
            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = wasapi.FriendlyName;
                devices[$"Wasapi: {name}"] = new WasapiOutFactory(wasapi.ID);
            }

            foreach (var asio in AsioOut.GetDriverNames())
            {
                devices[$"ASIO: {asio}"] = new AsioOutFactory(asio);
            }

            return devices;
        }

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }
    }
}