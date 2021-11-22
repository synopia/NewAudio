using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Block;
using NewAudio.Devices;
using Serilog;
using Serilog.Formatting.Display;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.NewAudio;
using Xt;

namespace NewAudio.Core
{
    public interface IAudioService : IDisposable
    {
        IXtPlatform Platform { get; }

        IXtService GetService(XtSystem system);
        IResourceHandle<IXtDevice> OpenDevice(XtSystem system, string id);
        void CloseDevice(string id);
        int GetNextId();
    }

    public class AudioService : IAudioService
    {
        private ILogger _logger = Resources.GetLogger<AudioService>();
        private int _nextId;
        public IXtPlatform Platform { get; }
        
        private Dictionary<XtSystem, IXtService> _services = new ();
        private Dictionary<string, IResourceHandle<IXtDevice>> _devices = new ();
        
        public AudioService(IXtPlatform platform)
        {
            _logger.Information("============================================");
            _logger.Information("Initializing Audio Service");
            Platform = platform;
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

        public IResourceHandle<IXtDevice> OpenDevice(XtSystem system, string id)
        {
            var device = ResourceProvider.NewPooledSystemWide($"Device.{id}", _ => GetService(system).OpenDevice(id)).Finally(
                d =>
                {
                    CloseDevice(id);
                });
            var handle = device.GetHandle();
            _devices[id] = handle;
            return handle;
        }

        public void CloseDevice(string id)
        {
            var handle = _devices[id];
            handle?.Dispose();
            _devices.Remove(id);
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
                    foreach (var device in _devices.ToArray())
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