using System;
using System.Collections.Generic;
using NewAudio.Core;
using VL.Lib;
using VL.Lib.Collections;

namespace NewAudio.Devices
{
    [Serializable]
    public class OutputDeviceSelection : DynamicEnumBase<OutputDeviceSelection, OutputDeviceDefinition>
    {
        public OutputDeviceSelection(string value) : base(value)
        {
        }

        public static OutputDeviceSelection CreateDefault()
        {
            return CreateDefaultBase("Null: Output");
        }
    }


    public class OutputDeviceDefinition : DynamicEnumDefinitionBase<OutputDeviceDefinition>
    {
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var devices = new Dictionary<string, object>();
            // todo
            var driverManager = Factory.Instance.GetDriverManager().Resource;

            foreach (var device in driverManager.GetOutputDevices())
            {
                devices[device.ToString()] = null;
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