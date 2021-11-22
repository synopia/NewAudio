using System;
using Xt;

namespace NewAudio.Core
{
    public delegate int XtOnBuffer(IXtStream stream, in XtBuffer buffer, object user);
    public interface IXtDevice : IDisposable
    {
        XtBufferSize GetBufferSize(in XtFormat format);
        int GetChannelCount(bool output);
        XtMix? GetMix();
        IXtStream OpenStream(in XtDeviceStreamParams param, object user);
        bool SupportsAccess(bool interleaved);
        bool SupportsFormat(in XtFormat format);
        string GetChannelName(bool output, int index);
    }

    public interface IXtStream : IDisposable
    {
        XtFormat GetFormat();
        int GetFrames();
        XtLatency GetLatency();
        bool IsRunning();
        void Start();
        void Stop();
    }
    
    public interface IXtService
    {
        IXtDevice OpenDevice(string id);
        IXtDeviceList OpenDeviceList(XtEnumFlags flags);
        string GetDefaultDeviceId(bool output);
    }

    public interface IXtDeviceList : IDisposable
    {
        int GetCount();
        string GetId(int index);
        string GetName(string id);
        XtDeviceCaps GetCapabilities(string id);
    }

    public interface IXtPlatform : IDisposable
    {
        IXtService GetService(XtSystem system);
    }
}