using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Block;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class DeviceManager
    {
        private readonly List<DeviceSelection> _devices = new();
        private readonly List<IDriver> _drivers = new();
        private readonly List<IDevice> _activeDevices = new();

        public DeviceManager()
        {
            Init();
        }

        public void Init()
        {
            _activeDevices.Clear();

            _drivers.Clear();
            _devices.Clear();
            
            _drivers.Add(new AsioDriver());
            // _drivers.Add(new WasapiDriver());
            // _drivers.Add(new DirectSoundDriver());
            // _drivers.Add(new WaveDriver());

            foreach (var driver in _drivers)
            {
                _devices.AddRange(driver.GetDeviceSelections());
            }

            _devices.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));
            // _drivers.Insert(0, new NullDriver());
            // _devices.InsertRange(0, new NullDriver().GetDeviceSelections());
        }

                
        public void UpdateAllDevices()
        {
            lock (_activeDevices)
            {
                foreach (var device in _activeDevices)
                {
                    device.Update();
                }
            }
        }

        private IResourceHandle<IDevice> GetHandle(string name)
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
                    
                    var device = driver.CreateDevice(selection);
                    lock (_activeDevices)
                    {
                        _activeDevices.Add(device);
                    }
                    return device;
                }, 0).Finally(d =>
            {
                lock (_activeDevices)
                {
                    _activeDevices.Remove(d);
                }
            });

            return pool.GetHandle();
        }

        public VirtualInput GetInputDevice(InputDeviceSelection inputDeviceSelection, AudioBlockFormat format)
        {
            var resource = GetHandle(inputDeviceSelection.Value);
            if (resource == null)
            {
                return null;
            }

            var virtualDevice = new VirtualInput(resource, format);
            return virtualDevice;
        }

        public VirtualOutput GetOutputDevice(OutputDeviceSelection outputDeviceSelection, AudioBlockFormat format)
        {
            var resource = GetHandle(outputDeviceSelection.Value);
            if (resource == null)
            {
                return null;
            }

            var virtualDevice = new VirtualOutput(resource, format);
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