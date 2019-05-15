using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VL.Lib;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    [Serializable]
    public class WaveInputDevice : DynamicEnumBase<WaveInputDevice, WaveInputDeviceDefinition>
    {
        public WaveInputDevice(string value) : base(value)
        {
        }

        public static WaveInputDevice CreateDefault() => CreateDefaultBase("No audio input device found");
    }

    public interface IWaveInputFactory
    {
        IWaveIn Create(int latency);
    }

    public class WaveInFactory : IWaveInputFactory
    {
        private int deviceId;

        public WaveInFactory(int deviceId)
        {
            this.deviceId = deviceId;
        }

        public IWaveIn Create(int latency)
        {
            var waveIn = new WaveInEvent {DeviceNumber = deviceId, BufferMilliseconds = latency};
            return waveIn;
        }
    }

    public class WasapiInFactory : IWaveInputFactory
    {
        private string deviceId;

        public WasapiInFactory(string deviceId)
        {
            this.deviceId = deviceId;
        }

        public IWaveIn Create(int latency)
        {
            var device = new MMDeviceEnumerator().GetDevice(deviceId);
            var wasapi = new WasapiCapture(device);
            return wasapi;
        }
    }

    public class WasapiLoopbackFactory : IWaveInputFactory
    {
        private string deviceId;

        public WasapiLoopbackFactory(string deviceId)
        {
            this.deviceId = deviceId;
        }

        public IWaveIn Create(int latency)
        {
            var device = new MMDeviceEnumerator().GetDevice(deviceId);
            var wasapi = new WasapiLoopbackCapture(device);
            return wasapi;
        }
    }

    public class WaveInputDeviceDefinition : DynamicEnumDefinitionBase<WaveInputDeviceDefinition>
    {
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            Dictionary<string, object> devices = new Dictionary<string, object>();

            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = caps.ProductName;
                devices[name] = i;
            }

            var enumerator = new MMDeviceEnumerator();
            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                var name = wasapi.FriendlyName;
                devices[$"Wasapi: {name}"] = new WasapiInFactory(wasapi.ID);
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = wasapi.FriendlyName;
                devices[$"Wasapi Loopback: {name}"] = new WasapiLoopbackFactory(wasapi.ID);
            }

            return devices;
        }

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }
    }
}