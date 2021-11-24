using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Block;
using NewAudio.Devices;
using Serilog;
using Serilog.Formatting.Display;
using VL.Core;
using VL.Lang;
using VL.Lib.Basics.Resources;
using VL.Model;
using VL.NewAudio;
using Xt;

namespace NewAudio.Core
{
    public interface IAudioService : IDisposable
    {
        IXtPlatform Platform { get; }

        IXtService GetService(XtSystem system);
        IResourceHandle<Device> OpenDevice(XtSystem system, string id);
        void CloseDevice(string id);
        int GetNextId();
        IEnumerable<DeviceSelection> GetInputDevices();
        IEnumerable<DeviceSelection> GetOutputDevices();
    }

    public class AudioService : IAudioService
    {
        private ILogger _logger = Resources.GetLogger<AudioService>();
        private int _nextId;
        public IXtPlatform Platform { get; }
        private readonly List<DeviceSelection> _deviceSelections = new();
       
        private Dictionary<XtSystem, IXtService> _services = new ();
        private Dictionary<string, IResourceHandle<IXtDevice>> _xtDevices = new ();

        private Message _currentMessage;
        
        public AudioService(IXtPlatform platform)
        {
            platform.OnError += (msg) =>
            {
                _logger.Error("Error: {Message}", msg);
            };
            _logger.Information("============================================");
            _logger.Information("Initializing Audio Service");
            Platform = platform;
            InitSelectionEnums();
            _logger.Information("Found {Inputs} input and {Outputs} output devices", GetInputDevices().Count(), GetOutputDevices().Count());
            _currentMessage = new Message(MessageSeverity.Error, "Audio ON");
        }
        
        public IXtService GetService(XtSystem system)
        {
            if (_services.ContainsKey(system))
            {
                return _services[system];
            }
            var service =  Platform.GetService(system);
            _services[system] = service;

            return service;
        }

        public void InitSelectionEnums()
        {
            _deviceSelections.Clear();
            var systems = new[] { XtSystem.ASIO, XtSystem.WASAPI, XtSystem.DirectSound };
            foreach (var system in systems)
            {
                using var list = GetService(system).OpenDeviceList(XtEnumFlags.All);

                for (int d = 0; d < list.GetCount(); d++)
                {
                    string id = list.GetId(d);
                    var caps = list.GetCapabilities(id);
                
                    _deviceSelections.Add(new DeviceSelection(system, id, list.GetName(id), (caps & XtDeviceCaps.Input) != 0, (caps & XtDeviceCaps.Output) != 0));                
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
            return _deviceSelections.Where(d => d.IsInputDevice);
        }

        public IEnumerable<DeviceSelection> GetOutputDevices()
        {
            return _deviceSelections.Where(d => d.IsOutputDevice);
        }
        
        public IResourceHandle<Device> OpenDevice(XtSystem system, string id)
        {
            var name = _deviceSelections.First(ds => ds.Id == id).Name;
            var key = $"Device.{id}";
            var xtDevice = ResourceProvider.NewPooledSystemWide(key, _ => GetService(system).OpenDevice(id)).Finally(
                d =>
                {
                    CloseDevice(id);
                });
            var xtHandle = xtDevice.GetHandle();
            _xtDevices[id] = xtHandle;
            IResourceProvider<Device> device;
            device = ResourceProvider.NewPooledSystemWide( key, _ => new Device(name, xtHandle));
            return device.GetHandle();
        }

        public void CloseDevice(string id)
        {
            var handle = _xtDevices[id];
            handle?.Dispose();
            _xtDevices.Remove(id);
        }
        
        public int GetNextId()
        {
            return _nextId++;
        }
        
        public void Dispose()
        {
            Dispose(true);
        }

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.Information("Disposing AudioService");
                    foreach (var device in _xtDevices.ToArray())
                    {
                        device.Value?.Dispose();
                    }
                    Platform.Dispose();
                    _logger.Information("==============================");

                }

                _disposedValue = disposing;
            }
        }
    }
}