using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Core;
using Xt;

namespace NewAudioTest
{

    public class TestStream : IXtStream
    {
        private bool _isRunning;
        public readonly XtDeviceStreamParams Param;

        public TestStream(XtDeviceStreamParams param)
        {
            Param = param;
        }

        public void Dispose()
        {
            
        }

        public XtStream GetXtStream()
        {
            return null;
        }

        public XtFormat GetFormat()
        {
            return Param.format;
        }

        public int GetFrames()
        {
            return 512;
        }

        public XtLatency GetLatency()
        {
            return new XtLatency();
        }

        public bool IsRunning()
        {
            return _isRunning;
        }

        public void Start()
        {
            _isRunning = true;
        }

        public void Stop()
        {
            _isRunning = false;

        }
    }
    public class TestDevice: IXtDevice
    {
        public XtSystem System;
        public string Id;
        public string Name;
        public XtDeviceCaps Caps;
        public bool IsDefault;
        private List<XtFormat> _formats;
        public int Inputs;
        public int Outputs;
        public bool Interleaved;
        public Action OnDispose;
        public Func<XtDeviceStreamParams,IXtStream> OnOpenStream;
        

        public TestDevice(XtSystem system, string id, string name, XtDeviceCaps caps, bool isDefault, IEnumerable<XtFormat> formats)
        {
            System = system;
            Id = id;
            Name = name;
            Caps = caps;
            IsDefault = isDefault;
            _formats = new (formats);
        }

        public void Dispose()
        {
            OnDispose?.Invoke();
        }

        public XtBufferSize GetBufferSize(in XtFormat format)
        {
            return new XtBufferSize();
        }

        public int GetChannelCount(bool output)
        {
            return output ? Outputs : Inputs;
        }

        public XtMix? GetMix()
        {
            return _formats.First().mix;
        }

        public IXtStream OpenStream(in XtDeviceStreamParams param, object user)
        {
            return OnOpenStream!=null ? OnOpenStream.Invoke(param) : new TestStream(param);
        }

        public bool SupportsAccess(bool interleaved)
        {
            return Interleaved == interleaved;
        }

        public bool SupportsFormat(in XtFormat format)
        {
            return _formats.Contains(format);
        }

        public string GetChannelName(bool output, int index)
        {
            return "";
        }
    }
    
    public class TestDeviceList : IXtDeviceList
    {
        private IList<TestDevice> _devices;

        public TestDeviceList(IList<TestDevice> devices)
        {
            _devices = devices;
        }

        public void Dispose()
        {
        }

        public int GetCount()
        {
            return _devices.Count();
        }

        public string GetId(int index)
        {
            return _devices[index].Id;
        }

        public string GetName(string id)
        {
            return _devices.First(d=>d.Id==id).Name;
        }

        public XtDeviceCaps GetCapabilities(string id)
        {
            return _devices.First(d => d.Id == id).Caps;
        }
    }
    public class TestPlatform : IXtPlatform
    {
        private IList<TestDevice> _devices;

        public TestPlatform(IList<TestDevice> devices)
        {
            _devices = devices;
        }

        public void Dispose()
        {
        }

        public IXtService GetService(XtSystem system)
        {
            return new TestService(_devices.Where(d=>d.System==system).ToList());
        }

        public Action<string> OnError { get; set; }
        public void DoOnError(string message)
        {
            throw new NotImplementedException();
        }
    }
    public class TestService : IXtService
    {
        private IList<TestDevice> _devices;

        public TestService(IList<TestDevice> devices)
        {
            _devices = devices;
        }

        public IXtDevice OpenDevice(string id)
        {
            return _devices.First(d => d.Id == id);
        }

        public IXtDeviceList OpenDeviceList(XtEnumFlags flags)
        {
            return new TestDeviceList(_devices.ToList());
        }

        public string GetDefaultDeviceId(bool output)
        {
            return output
                ? _devices.FirstOrDefault(d => d.IsDefault && (d.Caps & XtDeviceCaps.Output) != 0)?.Id??""
                : _devices.FirstOrDefault(d => d.IsDefault && (d.Caps & XtDeviceCaps.Input) != 0)?.Id??"";
        }
    }

}