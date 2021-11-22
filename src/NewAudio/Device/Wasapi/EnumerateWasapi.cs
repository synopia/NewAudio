using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace NewAudio.Devices.Wasapi
{
    public class EnumerateWasapi
    {
        public static IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            var list = new List<DeviceSelection>();
            
            var enumerator = new MMDeviceEnumerator();

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                list.Add(new DeviceSelection(( dm)=>
                    new WasapiDevice(dm, wasapi.ID, wasapi.FriendlyName),
                    "Wasapi",  wasapi.FriendlyName, true, false)); 
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add(new DeviceSelection(( dm)=>new WasapiDevice(dm, wasapi.ID, wasapi.FriendlyName), "Wasapi Loopback", wasapi.FriendlyName, true, false));
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add(new DeviceSelection((dm)=>new WasapiDevice(dm, wasapi.ID, wasapi.FriendlyName), "Wasapi", wasapi.FriendlyName, false, true));
            }

            return list;
        }
    }
}