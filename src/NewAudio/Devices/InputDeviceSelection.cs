using System;
using System.Collections.Generic;
using NewAudio.Core;
using VL.Lib;
using VL.Lib.Basics.Resources;
using VL.Lib.Collections;

namespace NewAudio.Devices
{
    
    [Serializable]
    public class InputDeviceSelection : DynamicEnumBase<InputDeviceSelection, InputDeviceDefinition>
    {
        public InputDeviceSelection(string value) : base(value)
        {
        }

        public static InputDeviceSelection CreateDefault()
        {
            return CreateDefaultBase("Null: Input");
        }
        
    }

    public class InputDeviceDefinition : DynamicEnumDefinitionBase<InputDeviceDefinition>
    {
        public InputDeviceDefinition()
        {
            
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var devices = new Dictionary<string, object>();
            // todo
            var driverManager = Factory.Instance.GetDriverManager().Resource;

            foreach (var device in driverManager.GetInputDevices())
            {
                devices[device.Name] = null;
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