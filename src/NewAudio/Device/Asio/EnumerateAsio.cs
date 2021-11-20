using System.Collections.Generic;
using NAudio.Wave;

namespace NewAudio.Devices
{
    public class EnumerateAsio
    {
        public static IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            var list = new List<DeviceSelection>();

            foreach (var asio in AsioOut.GetDriverNames())
            {
                list.Add(new DeviceSelection((DriverManager dm)=>new AsioDriver(dm, asio), "ASIO", asio, true, true));
            }

            return list;
        }

    }
}