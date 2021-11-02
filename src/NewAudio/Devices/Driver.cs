using System.Collections.Generic;

namespace NewAudio.Devices
{
    public interface IDriver
    {
        public string Name { get; }


        public IEnumerable<IDevice> GetDevices();
    }
}