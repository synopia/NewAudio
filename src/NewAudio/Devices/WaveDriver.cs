using System.Collections.Generic;
using NAudio.Wave;

namespace NewAudio.Devices
{
    public class WaveDriver : IDriver
    {
        public string Name => "Wave";

        public IEnumerable<IDevice> GetDevices()
        {
            var list = new List<IDevice>();
            
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = caps.ProductName;
                list.Add(new WaveDevice($"{Name}: {name}", true, i));
            }
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                var name = caps.ProductName;
                list.Add(new WaveDevice($"{Name}: {name}", false, i));
            }

            return list;
        }

    }
}