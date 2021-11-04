using System;
using System.Collections.Generic;
using VL.Lib;
using VL.Lib.Collections;

namespace NewAudio.Devices
{
    [Serializable]
    public class WaveInputDevice : DynamicEnumBase<WaveInputDevice, WaveInputDeviceDefinition>
    {
        public WaveInputDevice(string value) : base(value)
        {
        }

        public static WaveInputDevice CreateDefault()
        {
            return CreateDefaultBase("Null: Input");
        }
    }

    public class WaveInputDeviceDefinition : DynamicEnumDefinitionBase<WaveInputDeviceDefinition>
    {
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var devices = new Dictionary<string, object>();

            foreach (var device in DriverManager.Instance.GetInputDevices())
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