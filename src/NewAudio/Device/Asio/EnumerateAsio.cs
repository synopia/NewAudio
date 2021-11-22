using System.Collections.Generic;
using NAudio.Wave;

namespace NewAudio.Devices.Asio
{
    public class EnumerateAsio
    {
        public static IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            var list = new List<DeviceSelection>();

            foreach (var asio in AsioOut.GetDriverNames())
            {
                list.Add(new DeviceSelection(( dm)=>
                    new AsioDevice(dm, asio), 
                    "ASIO", asio, true, true));
            }

            return list;
        }

    }
}