using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
 
    public class DriverManager
    {
        private readonly List<DeviceSelection> _devices = new();
        private readonly List<IDriver> _drivers = new();

        public DriverManager()
        {
            
            _drivers.Add(new AsioDriver());
            _drivers.Add(new WasapiDriver());
            // _drivers.Add(new DirectSoundDriver());
            // _drivers.Add(new WaveDriver());

            foreach (var driver in _drivers)
            {
                _devices.AddRange(driver.GetDeviceSelections());
            }

            _devices.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _devices.InsertRange(0, new NullDriver().GetDeviceSelections());
        }

        private IResourceHandle<IDevice> GetHandle(string name)
        {
            var selection = _devices.Find(d => d.Name == name);
            if (selection == null)
            {
                return null;
            }

            var driver = _drivers.Find(d => d.Name == selection.DriverName);
            if (driver == null)
            {
                return null;
            }
            var pool = ResourceProvider.NewPooledSystemWide($"{driver.Name}.{selection.Name}", s =>
            {
                return driver.CreateDevice(selection);
            });
            return pool.GetHandle();
        }
        public VirtualDevice GetInputDevice(InputDeviceSelection inputDeviceSelection)
        {
            var resource =  GetHandle(inputDeviceSelection.Value);
            var virtualDevice = new VirtualDevice(resource);
            return virtualDevice;
        }
        public VirtualDevice GetOutputDevice(OutputDeviceSelection outputDeviceSelection)
        {
            var resource =  GetHandle(outputDeviceSelection.Value);
            var virtualDevice = new VirtualDevice(resource);
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