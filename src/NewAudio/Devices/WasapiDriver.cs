using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace NewAudio.Devices
{
    public class WasapiDriver : IDriver
    {
        public string Name => "Wasapi";

        public IEnumerable<IDevice> GetDevices()
        {
            var list = new List<IDevice>();
            var enumerator = new MMDeviceEnumerator();
            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                var name = wasapi.FriendlyName;
                list.Add(new WasapiDevice($"Wasapi: {name}", true, false, wasapi.ID));
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = wasapi.FriendlyName;
                list.Add(new WasapiDevice($"Wasapi Loopback: {name}", true, true, wasapi.ID));
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = wasapi.FriendlyName;
                list.Add(new WasapiDevice($"Wasapi: {name}", false, false, wasapi.ID));
            }

            return list;
        }
    }
}