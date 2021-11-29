using System;
using System.Collections.Generic;
using VL.Lib;
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
            return CreateDefaultBase();
        }
    }

    public class InputDeviceDefinition : ManualDynamicEnumDefinitionBase<InputDeviceDefinition>
    {
        protected override void Initialize()
        {
            AddEntry("-", null);
        }

        public new static InputDeviceDefinition Instance =>
            ManualDynamicEnumDefinitionBase<InputDeviceDefinition>.Instance;

        protected override bool AutoSortAlphabetically => false;
    }
}