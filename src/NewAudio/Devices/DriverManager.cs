using System;
using System.Collections.Generic;
using System.Linq;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class DriverManager
    {
        private readonly List<DeviceSelection> _devices = new();
        private readonly List<IDriver> _drivers = new();
        private readonly HashSet<IResourceProvider<IDevice>> _pools = new();

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

            _devices.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));
            _drivers.Insert(0, new NullDriver());
            _devices.InsertRange(0, new NullDriver().GetDeviceSelections());
        }

        public List<IDevice> CheckPools()
        {
            var openDevices = new List<IDevice>();
            foreach (var pool in _pools)
            {
                if (pool.Monitor().SinkCount > 0)
                {
                    openDevices.AddRange(pool.Monitor().ResourcesUsedBySinks);
                }
            }

            return openDevices;
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
                s => { return driver.CreateDevice(selection); });
            _pools.Add(pool);
            return pool.GetHandle();
        }

        public VirtualDevice GetInputDevice(InputDeviceSelection inputDeviceSelection)
        {
            var resource = GetHandle(inputDeviceSelection.Value);
            if (resource == null)
            {
                return null;
            }

            var virtualDevice = new VirtualDevice(resource);
            return virtualDevice;
        }

        public VirtualDevice GetOutputDevice(OutputDeviceSelection outputDeviceSelection)
        {
            var resource = GetHandle(outputDeviceSelection.Value);
            if (resource == null)
            {
                return null;
            }

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