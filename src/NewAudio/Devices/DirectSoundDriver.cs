using System.Collections.Generic;
using NAudio.Wave;

namespace NewAudio.Devices
{
    public class DirectSoundDriver: IDriver
    {
        public string Name => "DirectSound";
        
        public IEnumerable<IDevice> GetDevices()
        {
            var list = new List<IDevice>();
            foreach (var device in DirectSoundOut.Devices)
            {
                var name = device.Description;
                list.Add(new DirectSoundDevice($"DS: {name}", device.Guid));
            }

            return list;
        }
    }
}