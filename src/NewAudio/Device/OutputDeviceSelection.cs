using System;
using System.Collections.Generic;
using NewAudio.Core;
using VL.Lib;
using VL.Lib.Collections;
using Xt;

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
            return CreateDefaultBase();
        }
    }


    public class OutputDeviceDefinition : ManualDynamicEnumDefinitionBase<OutputDeviceDefinition>
    {
        protected override void Initialize()
        {
        }

        public new static OutputDeviceDefinition Instance =>
            ManualDynamicEnumDefinitionBase<OutputDeviceDefinition>.Instance;
        protected override bool AutoSortAlphabetically => false;
    }
}