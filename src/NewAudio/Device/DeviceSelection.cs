namespace NewAudio.Devices
{
    public class DeviceSelection
    {
        public string Name { get; }
        public string DriverName { get; }
        public string NamePrefix { get; }

        public bool IsInputDevice { get; }
        public bool IsOutputDevice { get; }

        public DeviceSelection(string driverName, string namePrefix, string name, bool isInputDevice, bool isOutputDevice)
        {
            DriverName = driverName;
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