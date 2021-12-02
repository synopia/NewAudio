using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NAudio.CoreAudioApi;
using VL.Lib;
using Xt;

namespace NewAudio.Device
{
    public readonly struct DeviceName
    {
        public string Name { get; init; }
        public XtSystem System { get; init; }
        public string Id { get; init; }
        public bool IsInput { get; init; }
        public bool IsOutput { get; init; }
        
        public override string ToString()
        {
            var type = System switch
            {
                XtSystem.DirectSound => "DirectSound",
                XtSystem.ASIO => "ASIO",
                XtSystem.WASAPI => "Wasapi",
                _ => ""
            };
            return $"{type}: {Name}";
        }
    }

    public struct DeviceCaps
    {
        public string DeviceId;
        public string Name;
        public XtSystem System;
        public XtDeviceCaps Caps;
        public XtEnumFlags InOut;
            
        public int MaxOutputChannels;
        public int MaxInputChannels;

        public double BufferSizeMsMin;
        public double BufferSizeMsMax;

        public bool Interleaved;
        public bool NonInterleaved;

        public string Error;
    }

    public interface IAudioService: IDisposable
    {
        Subject<object> DevicesScanned { get; }
        String SystemName { get; }
        XtSystem System { get; }

        void ScanForDevices();
        
        IEnumerable<DeviceName> GetDevices();
        DeviceName GetDefaultDevice(bool output);
        // bool HasSeparateInputsAndOutputs();

        IAudioDevice? OpenDevice(string? deviceId);
    }

    public class XtAudioService : IAudioService
    {
        public Subject<object> DevicesScanned { get; } = new Subject<object>();
        public string SystemName => System.ToString();
        public XtSystem System { get; }
        private readonly List<DeviceName> _defaultDevices = new();
        private readonly List<DeviceName> _deviceSelections = new();
        private readonly Dictionary<string, DeviceCaps> _deviceCaps = new();
        
        private readonly Dictionary<XtSystem, XtService> _services = new();
        private XtPlatform _platform;

        public XtAudioService(XtPlatform platform)
        {
            _platform = platform;
            ScanForDevices();
        }

        public void Dispose()
        {
            
        }

        public IEnumerable<DeviceName> GetDevices()
        {
            return _deviceSelections;
        }

        // public bool HasSeparateInputsAndOutputs()
        // {
            // return _currentSelected.System != XtSystem.ASIO;
        // }

        private XtService GetService(XtSystem system)
        {
            if (_services.ContainsKey(system))
            {
                return _services[system];
            }

            var service = _platform.GetService(system);
            _services[system] = service;

            return service;
        }

        public DeviceName GetDefaultDevice(bool output)
        {
            if (output)
            {
                return GetDefaultOutputDevices().First();
            }
            return GetDefaultInputDevices().First();
        }

        public IAudioDevice? OpenDevice(string? deviceId)
        {
            if (deviceId==null || !_deviceCaps.ContainsKey(deviceId))
            {
                return null;
            }
            var caps = _deviceCaps[deviceId];
            return new XtAudioDevice(GetService(caps.System), caps);
        }
        
        // public AudioSession CreateSession(string outputDeviceId, string inputDeviceId)
        // {
            // var output = OpenDevice(outputDeviceId);
            // var input = outputDeviceId != inputDeviceId ? OpenDevice(inputDeviceId) : output;
            
            // return new AudioSession(input, output);
        // }

        public void ScanForDevices()
        {
              _deviceSelections.Clear();
            var systems = new[] { XtSystem.ASIO, XtSystem.WASAPI, XtSystem.DirectSound };
            // var systems = new[] {  XtSystem.DirectSound };
            foreach (var system in systems)
            {
                using var list = GetService(system).OpenDeviceList(XtEnumFlags.All);
                var outputDefault = GetService(system).GetDefaultDeviceId(true);
                var inputDefault = GetService(system).GetDefaultDeviceId(false);

                for (int d = 0; d < list.GetCount(); d++)
                {
                    string id = list.GetId(d);
                    try
                    {
                        using XtDevice device = GetService(system).OpenDevice(id);

                        var caps = list.GetCapabilities(id);

                        var deviceId = id;
                        var deviceName = new DeviceName()
                        {
                            System = system, Id = deviceId, Name = list.GetName(id),
                            IsInput = (caps & XtDeviceCaps.Input) != 0, IsOutput = (caps & XtDeviceCaps.Output) != 0
                        };
                        _deviceCaps[deviceId] = new DeviceCaps()
                            {
                                Caps = caps,
                                DeviceId = deviceId,
                                Name = list.GetName(id),
                                System = system,
                                MaxInputChannels = device.GetChannelCount(false),
                                MaxOutputChannels = device.GetChannelCount(true),
                                Interleaved = device.SupportsAccess(true),
                                NonInterleaved = device.SupportsAccess(false),
                        };
                        _deviceSelections.Add(deviceName);
                        if (id == outputDefault || id == inputDefault)
                        {
                            _defaultDevices.Add(deviceName);
                        }
                    }
                    catch (XtException e)
                    {
                        _deviceCaps[id] = new DeviceCaps()
                        {
                            Error = e.Message
                        };
                    }
                }
            }

            _deviceSelections.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));

            DeviceSelectionDefinition.Instance.Clear();
            foreach (var device in GetDevices())
            {
                DeviceSelectionDefinition.Instance.AddEntry(device.ToString(), device);
            }
        }
        public IEnumerable<DeviceName> GetInputDevices()
        {
            return _deviceSelections.Where(i => i.IsInput);
        }

        public IEnumerable<DeviceName> GetOutputDevices()
        {
            return _deviceSelections.Where(i => i.IsOutput);
        }

        public IEnumerable<DeviceName> GetDefaultInputDevices()
        {
            return _defaultDevices.Where(i => i.IsInput);
        }

        public IEnumerable<DeviceName> GetDefaultOutputDevices()
        {
            return _defaultDevices.Where(i => i.IsOutput);
        }

    }
}