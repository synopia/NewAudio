using System;
using System.Collections.Generic;
using NewAudio.Devices;
using Xt;

namespace NewAudio.Core
{
    public interface IAudioService : IDisposable
    {
        IEnumerable<DeviceSelection> GetInputDevices();
        IEnumerable<DeviceSelection> GetOutputDevices();
        IEnumerable<DeviceSelection> GetDefaultInputDevices();
        IEnumerable<DeviceSelection> GetDefaultOutputDevices();

        DeviceCaps GetDeviceInfo(DeviceSelection selection);

        string RegisterAudioGraph(BeforeDeviceConfigChange onBeforeDeviceConfigChange, AfterDeviceConfigChange onAfterDeviceConfigChange,BeforeAudioBufferFill beforeAudioBufferFill, AfterAudioBufferFill afterAudioBufferFill);
        void UnregisterAudioGraph(string graphId);
        
        Session OpenDevice(string deviceId, string graphId, ChannelConfig config, OnAudioBufferRequest onAudioBufferRequest);
        void CloseDevice(string sessionId);

        void OpenStream(string sessionId);
        void CloseStream(string sessionId);

        int UpdateFormat(string deviceId, FormatConfig config);
        List<Session> Sessions { get; }
        int OnBuffer(in XtBuffer deviceBuffer);
    }

}