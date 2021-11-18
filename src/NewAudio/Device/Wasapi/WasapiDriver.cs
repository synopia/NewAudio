using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace NewAudio.Devices
{
    public class WasapiDriver : IDriver
    {
        public string Name => "Wasapi";
        private Dictionary<string, string> map = new();

        public IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            var list = new List<DeviceSelection>();
            var enumerator = new MMDeviceEnumerator();

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                var name = $"Wasapi: {wasapi.FriendlyName}";
                map[name] = wasapi.ID;
                list.Add(new DeviceSelection(Name, Name, wasapi.FriendlyName, true, false)); //, wasapi.ID
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = $"Wasapi Loopback: {wasapi.FriendlyName}";
                map[name] = wasapi.ID;
                list.Add(new DeviceSelection(Name, "Wasapi Loopback", wasapi.FriendlyName, true, false));
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = $"Wasapi: {wasapi.FriendlyName}";
                map[name] = wasapi.ID;
                list.Add(new DeviceSelection(Name, Name, wasapi.FriendlyName, false, true));
            }

            return list;
        }

        public IDevice CreateDevice(DeviceSelection selection)
        {
            var wasapiId = map[selection.ToString()];
            var loopback = selection.ToString().StartsWith("Wasapi Loopback");
            return new WasapiDevice(selection.ToString(), selection.IsInputDevice, loopback, wasapiId);
        }
    }
}