﻿using System.Collections.Generic;
using NAudio.Wave;

namespace NewAudio.Devices
{
    public class AsioDriver : IDriver
    {
        public string Name => "ASIO";

        public IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            var list = new List<DeviceSelection>();

            foreach (var asio in AsioOut.GetDriverNames())
            {
                list.Add(new DeviceSelection(Name, Name, asio, true, true));
            }

            return list;
        }

        public IAudioClient CreateClient(DeviceSelection selection)
        {
            return new AsioClient(selection.Name, selection.Name);
        }
    }
}