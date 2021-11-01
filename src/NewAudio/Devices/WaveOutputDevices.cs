using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NewAudio.Devices;
using VL.Lib;
using VL.Lib.Collections;

namespace NewAudio.Devices
{
    [Serializable]
    public class WaveOutputDevice : DynamicEnumBase<WaveOutputDevice, WaveOutputDeviceDefinition>
    {
        public WaveOutputDevice(string value) : base(value)
        {
        }

        public static WaveOutputDevice CreateDefault() => CreateDefaultBase("Null: Output");
    }


    public class WaveOutputDeviceDefinition : DynamicEnumDefinitionBase<WaveOutputDeviceDefinition>
    {
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            Dictionary<string, object> devices = new Dictionary<string, object>();
            foreach (var device in DriverManager.Instance.GetOutputDevices())
            {
                devices[device.Name] = device;
            }
            return devices;
        }

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }
    }
}