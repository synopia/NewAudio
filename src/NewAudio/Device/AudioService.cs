using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using NewAudio.Devices;
using Xt;

namespace NewAudio.Device
{
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
        String SystemName { get; }
        XtSystem System { get; }

        void ScanForDevices();
        
        IEnumerable<DeviceSelection> GetDevices();
        DeviceSelection GetDefaultDevice(bool output);
        bool HasSeparateInputsAndOutputs();

        IAudioDevice CreateDevice(string outputDeviceId, string inputDeviceId);
    }

    public class XtAudioService : IAudioService
    {
        public string SystemName => System.ToString();
        public XtSystem System { get; }
        private readonly List<DeviceSelection> _defaultDevices = new();
        private readonly List<DeviceSelection> _deviceSelections = new();
        private readonly Dictionary<string, DeviceCaps> _deviceCaps = new();
        
        private readonly Dictionary<XtSystem, XtService> _services = new();
        private XtPlatform _platform;
        private DeviceCaps _currentSelected;

        public XtAudioService(XtPlatform platform)
        {
            _platform = platform;
        }

        public void Dispose()
        {
            
        }

        public IEnumerable<DeviceSelection> GetDevices()
        {
            return _deviceSelections;
        }

        public bool HasSeparateInputsAndOutputs()
        {
            return _currentSelected.System != XtSystem.ASIO;
        }

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

        public DeviceSelection GetDefaultDevice(bool output)
        {
            if (output)
            {
                return GetDefaultOutputDevices().First();
            }
            return GetDefaultInputDevices().First();
        }

        
        public IAudioDevice CreateDevice(string outputDeviceId, string inputDeviceId)
        {
            var caps = _deviceCaps[outputDeviceId];
            _currentSelected = caps;
            return new XtAudioDevice(GetService(caps.System), caps);
        }

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
                        var deviceSelection = new DeviceSelection(system, deviceId, list.GetName(id),
                            (caps & XtDeviceCaps.Input) != 0, (caps & XtDeviceCaps.Output) != 0);
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
                        _deviceSelections.Add(deviceSelection);
                        if (id == outputDefault || id == inputDefault)
                        {
                            _defaultDevices.Add(deviceSelection);
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

            OutputDeviceDefinition.Instance.Clear();
            foreach (var selection in GetOutputDevices())
            {
                OutputDeviceDefinition.Instance.AddEntry(selection.ToString(), selection);
            }

            InputDeviceDefinition.Instance.Clear();
            foreach (var selection in GetInputDevices())
            {
                InputDeviceDefinition.Instance.AddEntry(selection.ToString(), selection);
            }
        }
        public IEnumerable<DeviceSelection> GetInputDevices()
        {
            return _deviceSelections.Where(i => i.IsInputDevice);
        }

        public IEnumerable<DeviceSelection> GetOutputDevices()
        {
            return _deviceSelections.Where(i => i.IsOutputDevice);
        }

        public IEnumerable<DeviceSelection> GetDefaultInputDevices()
        {
            return _defaultDevices.Where(i => i.IsInputDevice);
        }

        public IEnumerable<DeviceSelection> GetDefaultOutputDevices()
        {
            return _defaultDevices.Where(i => i.IsOutputDevice);
        }

    }
}