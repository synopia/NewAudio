using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class DriverManager
    {
        private readonly IResourceHandle<AudioService> _audioService;
        private readonly List<DeviceSelection> _devices = new();
        private readonly List<IDriver> _drivers = new();

        private readonly List<IAudioClient> _activeClients = new();

        public DriverManager()
        {
            _audioService = Factory.GetAudioService();
            Init();
        }

        public void Init()
        {
            _drivers.Clear();
            _devices.Clear();
            _activeClients.Clear();
            
            _drivers.Add(new AsioDriver());
            _drivers.Add(new WasapiDriver());
            // _drivers.Add(new DirectSoundDriver());
            // _drivers.Add(new WaveDriver());

            foreach (var driver in _drivers)
            {
                _devices.AddRange(driver.GetDeviceSelections());
            }

            _devices.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));
            _drivers.Insert(0, new NullDriver());
            _devices.InsertRange(0, new NullDriver().GetDeviceSelections());
        }

        public void UpdateAllDevices()
        {
            lock (_activeClients)
            {
                foreach (var client in _activeClients)
                {
                    client.Update();
                }
            }
        }
        
        private IResourceHandle<IAudioClient> GetHandle(string name)
        {
            var selection = _devices.Find(d => d.ToString() == name);
            if (selection == null)
            {
                return null;
            }

            var driver = _drivers.Find(d => d.Name == selection.DriverName);
            if (driver == null)
            {
                return null;
            }

            var pool = ResourceProvider.NewPooledSystemWide(selection.ToString(),
                s =>
                {
                    var client = driver.CreateClient(selection);
                    lock (_activeClients)
                    {
                        _activeClients.Add(client);
                    }
                    return client;
                }, 0).Finally(d =>
            {
                lock (_activeClients)
                {
                    _activeClients.Remove(d);
                }
            });
            return pool.GetHandle();
        }

        public VirtualInput GetInputDevice(InputDeviceSelection inputDeviceSelection, DeviceConfigRequest config)
        {
            var resource = GetHandle(inputDeviceSelection.Value);
            if (resource == null)
            {
                return null;
            }

            var virtualDevice = new VirtualInput(resource, config);
            return virtualDevice;
        }

        public VirtualOutput GetOutputDevice(OutputDeviceSelection outputDeviceSelection, DeviceConfigRequest config)
        {
            var resource = GetHandle(outputDeviceSelection.Value);
            if (resource == null)
            {
                return null;
            }

            var virtualDevice = new VirtualOutput(resource, config);
            return virtualDevice;
        }


        public void AddDriver(IDriver driver)
        {
            _drivers.Add(driver);
            _devices.AddRange(driver.GetDeviceSelections());
        }

        public IEnumerable<DeviceSelection> GetInputDevices()
        {
            return _devices.Where(d => d.IsInputDevice);
        }

        public IEnumerable<DeviceSelection> GetOutputDevices()
        {
            return _devices.Where(d => d.IsOutputDevice);
        }
    }
}