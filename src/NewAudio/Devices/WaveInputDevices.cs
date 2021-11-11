using System;
using System.Collections.Generic;
using NewAudio.Core;
using VL.Lib;
using VL.Lib.Basics.Resources;
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
        public WaveInputDeviceDefinition()
        {
            
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var devices = new Dictionary<string, object>();
            // todo
            var driverManager = VLApi.Instance.GetDriverManager().Resource;

            foreach (var device in driverManager.GetInputDevices())
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