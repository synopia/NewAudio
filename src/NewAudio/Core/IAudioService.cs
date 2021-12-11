using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using VL.Lib.Basics.Resources;
using Xt;

namespace VL.NewAudio.Core
{
    public interface IAudioService : IDisposable
    {
        void ScanForDevices();

        IEnumerable<DeviceName> GetDevices();
        DeviceName GetDefaultDevice(bool output);

        IResourceHandle<IAudioDevice> OpenDevice(string deviceName);
    }
}