using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using VL.Lib;
using VL.Lib.Collections;
using Xt;

namespace NewAudio.Device
{
    [Serializable]
    public class DeviceSelection : DynamicEnumBase<DeviceSelection, DeviceSelectionDefinition>
    {

        public static DeviceSelection CreateDefault()
        {
            return CreateDefaultBase();
        }

        public DeviceSelection(string value) : base(value)
        {
        }
    }

    public class DeviceSelectionDefinition : ManualDynamicEnumDefinitionBase<DeviceSelectionDefinition>
    {
        public new static DeviceSelectionDefinition Instance =>
            ManualDynamicEnumDefinitionBase<DeviceSelectionDefinition>.Instance;

        /*
        protected override void Initialize()
        {
            _audioService = Resources.GetAudioService();
        }
        protected override bool AutoSortAlphabetically => false;
        
        
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var result = new Dictionary<string, object>();

            foreach (var device in _audioService.GetDevices())
            {
                result[device.ToString()] = device;
            }

            return result;
        }

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return _audioService.DevicesScanned;
        }
    */
    }
}