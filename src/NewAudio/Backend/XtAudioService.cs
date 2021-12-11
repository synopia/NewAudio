using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using VL.Lib.Basics.Resources;
using VL.NewAudio.Core;
using VL.NewAudio.Internal;
using Xt;

namespace VL.NewAudio.Backend
{
    public class XtAudioService : ErrorSupport, IAudioService
    {
        private readonly ILogger _logger = Resources.GetLogger<XtAudioService>();
        private readonly XtPlatform _platform;

         
        private readonly List<DeviceName> _defaultDevices = new();
        private readonly List<DeviceName> _deviceSelections = new();
        private readonly Dictionary<string, DeviceCaps> _deviceCaps = new();
        private readonly Dictionary<XtSystem, XtService> _services = new();
        private bool _disposed;

        public XtAudioService()
        {
            
            _platform = XtAudio.Init("NewAudio", IntPtr.Zero);
            XtAudio.SetOnError(OnError);
            _logger.Information("==============================================");
            _logger.Information("Starting AudioService");
        }

        private void OnError(string message)
        {
            _logger.Error("XtAudio.OnError: {Message}", message);
            AddError(message);
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.Information("Disposing AudioService");
                _disposed = true;

                _platform.Dispose();
            }
        }

        public IEnumerable<DeviceName> GetDevices()
        {
            return _deviceSelections;
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

        public DeviceName GetDefaultDevice(bool output)
        {
            if (output)
            {
                return GetDefaultOutputDevices().First();
            }
            return GetDefaultInputDevices().First();
        }

        public IResourceHandle<IAudioDevice> OpenDevice(string deviceName)
        {
            if (!_deviceCaps.ContainsKey(deviceName))
            {
                throw new InvalidOperationException($"Device {deviceName} not found!");
            }

            return ResourceProvider.NewPooledSystemWide(deviceName, (id) =>
            {
                _logger.Information("Create audio device {Id}", id);
                var caps = _deviceCaps[id];
                return new XtAudioDevice(GetService(caps.System), caps);
            }).GetHandle();
        }
        
        public void ScanForDevices()
        {
            _deviceSelections.Clear();
            var systems = new[] { XtSystem.ASIO, XtSystem.WASAPI, XtSystem.DirectSound };
            foreach (var system in systems)
            {
                using var list = GetService(system).OpenDeviceList(XtEnumFlags.All);
                var outputDefault = GetService(system).GetDefaultDeviceId(true);
                var inputDefault = GetService(system).GetDefaultDeviceId(false);

                for (int d = 0; d < list.GetCount(); d++)
                {
                    string id = list.GetId(d);
                    if (id == null)
                    {
                        continue;
                    }
                    try
                    {
                        using XtDevice device = GetService(system).OpenDevice(id);

                        var caps = list.GetCapabilities(id);

                        var deviceId = id;
                        var deviceName = new DeviceName(list.GetName(id), system, deviceId,
                            (caps & XtDeviceCaps.Input) != 0, (caps & XtDeviceCaps.Output) != 0,
                            id == outputDefault || id == inputDefault
                        );
                        _deviceCaps[deviceName.Name] = new DeviceCaps(deviceName)
                        {
                            Caps = caps,
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
                        _deviceCaps.Remove(id);
                        AddError(e.Message);
                    }
                }
            }

            _deviceSelections.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));

            DeviceSelectionDefinition.Instance.Clear();
            foreach (var device in GetDevices())
            {
                DeviceSelectionDefinition.Instance.AddEntry(device.ToString(), device);
            }

            _logger.Information("{Devices} devices found", _deviceCaps.Count);
            foreach (var service in _services)
            {
                _logger.Information("{System}: {Count} devices", service.Key, _deviceCaps.Count(c=>c.Value.System==service.Key));
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