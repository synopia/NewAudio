using System;

namespace NewAudio.Devices
{
    public class DeviceSelection
    {
        public string Name { get; }
        public Func<DriverManager, IDriver> Factory { get; }
        public string NamePrefix { get; }

        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }

        public DeviceSelection(Func<DriverManager, IDriver> factory, string namePrefix, string name, bool isInputDevice, bool isOutputDevice)
        {
            Factory = factory;
            Name = name;
            NamePrefix = namePrefix;
            IsInputDevice = isInputDevice;
            IsOutputDevice = isOutputDevice;
        }

        public override string ToString()
        {
            return $"{NamePrefix}: {Name}";
        }
    }
}