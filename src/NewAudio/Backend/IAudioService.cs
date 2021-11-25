using System;
using System.Collections.Generic;
using NewAudio.Devices;

namespace NewAudio.Core
{
    public interface IAudioService : IDisposable
    {
        IEnumerable<DeviceSelection> GetInputDevices();
        IEnumerable<DeviceSelection> GetOutputDevices();
        IEnumerable<DeviceSelection> GetDefaultInputDevices();
        IEnumerable<DeviceSelection> GetDefaultOutputDevices();

        DeviceCaps GetDeviceInfo(DeviceSelection selection);
        
        Session OpenDevice(string deviceId, ChannelConfig config);
        void CloseDevice(string sessionId);

        void OpenStream(string sessionId);
        void CloseStream(string sessionId);

        int UpdateFormat(string deviceId, FormatConfig config);
    }

}