using System.Collections.Generic;
using NAudio.Wave;

namespace NewAudio.Devices
{
    public class AsioDriver : IDriver
    {
        public string Name => "ASIO";
        
        public IEnumerable<IDevice> GetDevices()
        {
            var list = new List<IDevice>();

            foreach (var asio in AsioOut.GetDriverNames())
            {
                list.Add(new AsioDevice($"ASIO: {asio}", asio));
            }

            return list;
        }
    }
}