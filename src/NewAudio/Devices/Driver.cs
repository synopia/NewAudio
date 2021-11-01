using System.Collections;
using System.Collections.Generic;
using NAudio.Wave;

namespace NewAudio.Devices
{
    public interface IDriver
    {
        public string Name { get; }


        public IEnumerable<IDevice> GetDevices();
    }
}