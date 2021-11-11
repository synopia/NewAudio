﻿using System.Collections.Generic;

namespace NewAudio.Devices
{
    public class NullDriver : IDriver
    {
        public string Name => "Null";

        public IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            return new List<DeviceSelection>
            {
                new(Name, "Null: Input", true, false),
                new(Name, "Null: Output", false, true)
            };
        }

        public IDevice CreateDevice(DeviceSelection selection)
        {
            return new NullDevice(selection.Name, selection.IsInputDevice, selection.IsOutputDevice);
        }
    }
}