using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using VL.Lib.Basics.Resources;
using Xt;

namespace VL.NewAudio.Device
{
    

    public interface IAudioService: IDisposable
    {
        Subject<object> DevicesScanned { get; }

        void ScanForDevices();
        
        IEnumerable<DeviceName> GetDevices();
        DeviceName GetDefaultDevice(bool output);

        IResourceHandle<IAudioDevice> OpenDevice(string deviceId);
    }
}