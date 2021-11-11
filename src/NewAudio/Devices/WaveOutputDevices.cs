using System;
using System.Collections.Generic;
using NewAudio.Core;
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

        public static WaveOutputDevice CreateDefault()
        {
            return CreateDefaultBase("Null: Output");
        }
    }


    public class WaveOutputDeviceDefinition : DynamicEnumDefinitionBase<WaveOutputDeviceDefinition>
    {
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var devices = new Dictionary<string, object>();
            // todo
            var driverManager = VLApi.Instance.GetDriverManager().Resource;

            foreach (var device in driverManager.GetOutputDevices())
            {
                devices[device.Name] = device;
            }

            return devices;
        }

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }
        protected override bool AutoSortAlphabetically => false;

    }
}