using System;
using System.Collections.Generic;
using NAudio.Wave;
using VL.Lib;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public static class StaticNodes
    {
        [Serializable]
        public class WaveInputDevice : DynamicEnumBase<WaveInputDevice, WaveInputDeviceDefinition>
        {
            public WaveInputDevice(string value) : base(value)
            {
            }

            public static WaveInputDevice CreateDefault() => CreateDefaultBase("No audio input device found");
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

                return devices;
            }

            protected override IObservable<object> GetEntriesChangedObservable()
            {
                return HardwareChangedEvents.HardwareChanged;
            }

        }

        
    }
}