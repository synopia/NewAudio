using System;
using Xt;

namespace NewAudio.Devices
{
    public class DeviceSelection
    {
        public string Name { get; }
        public string DeviceId { get; }
        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }
        public XtSystem System { get; }

        public DeviceSelection(XtSystem system, string id, string name, bool isInputDevice, bool isOutputDevice)
        {
            DeviceId = id;
            Name = name;
            System = system;
            IsInputDevice = isInputDevice;
            IsOutputDevice = isOutputDevice;
        }

        public override string ToString()
        {
            var type = System switch
            {
                XtSystem.DirectSound => "DirectSound",
                XtSystem.ASIO => "ASIO",
                XtSystem.WASAPI => "Wasapi",
                _ => ""
            };
            return $"{type}: {Name}";
        }
    }
}