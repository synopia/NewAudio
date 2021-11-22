using System;
using Xt;

namespace NewAudio.Core
{
    public class RDevice :IXtDevice
    {
        private XtDevice _device;

        public RDevice(XtDevice device)
        {
            _device = device;
        }

        public XtBufferSize GetBufferSize(in XtFormat format)
        {
            return _device.GetBufferSize(format);
        }

        public int GetChannelCount(bool output)
        {
            return _device.GetChannelCount(output);
        }

        public XtMix? GetMix()
        {
            return _device.GetMix();
        }

        public IXtStream OpenStream(in XtDeviceStreamParams param, object user)
        {
            return new RStream(_device.OpenStream(param, user));
        }

        public bool SupportsAccess(bool interleaved)
        {
            return _device.SupportsAccess(interleaved);
        }

        public bool SupportsFormat(in XtFormat format)
        {
            return _device.SupportsFormat(format);
        }

        public string GetChannelName(bool output, int index)
        {
            return _device.GetChannelName(output, index);
        }

        public void Dispose()
        {
            _device.Dispose();
        }
    }

    public class RStream : IXtStream
    {
        private XtStream _stream;

        public RStream(XtStream stream)
        {
            _stream = stream;
        }

        public XtFormat GetFormat()
        {
            return _stream.GetFormat();
        }

        public int GetFrames()
        {
            return _stream.GetFrames();
        }

        public XtLatency GetLatency()
        {
            return _stream.GetLatency();
        }

        public bool IsRunning()
        {
            return _stream.IsRunning();
        }

        public void Start()
        {
            _stream.Start();
        }

        public void Stop()
        {
            _stream.Stop();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
    
    public class RService: IXtService
    {
        private XtService _service;

        public RService(XtService service)
        {
            _service = service;
        }

        public string GetDefaultDeviceId(bool output)
        {
            return _service.GetDefaultDeviceId(output);
        }

        public IXtDevice OpenDevice(string id)
        {
            return new RDevice(_service.OpenDevice(id));
        }

        public IXtDeviceList OpenDeviceList(XtEnumFlags flags)
        {
            return new RDeviceList(_service.OpenDeviceList(flags));
        }
        
    }

    public class RDeviceList :  IXtDeviceList
    {
        private XtDeviceList _list;

        public RDeviceList(XtDeviceList list)
        {
            _list = list;
        }

        public int GetCount()
        {
            return _list.GetCount();
        }

        public XtDeviceCaps GetCapabilities(string id)
        {
            return _list.GetCapabilities(id);
        }

        public string GetId(int index)
        {
            return _list.GetId(index);
        }

        public string GetName(string id)
        {
            return _list.GetName(id);
        }

        public void Dispose()
        {
            _list.Dispose();
        }
    }

    public class RPlatform : IXtPlatform
    {
        private XtPlatform _platform;

        public RPlatform(XtPlatform platform)
        {
            _platform = platform;
        }

        public IXtService GetService(XtSystem system)
        {
            return new RService(_platform.GetService(system));
        }

        public void Dispose()
        {
            _platform?.Dispose();
            _platform = null;
        }
    }
}