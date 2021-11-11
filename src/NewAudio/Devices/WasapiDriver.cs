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
                list.Add(new (Name, name, true, false)); //, wasapi.ID
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = $"Wasapi Loopback: {wasapi.FriendlyName}";
                map[name] = wasapi.ID;
                list.Add(new(Name, name, true, false));
            }

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = $"Wasapi: {wasapi.FriendlyName}";
                map[name] = wasapi.ID;
                list.Add(new(Name, name, false, true));
            }

            return list;
        }

        public IDevice CreateDevice(DeviceSelection selection)
        {
            var wasapiId = map[selection.Name];
            var loopback = wasapiId.StartsWith("Wasapi Loopback");
            return new WasapiDevice(selection.Name, selection.IsInputDevice, loopback, wasapiId);
        }
    }
}