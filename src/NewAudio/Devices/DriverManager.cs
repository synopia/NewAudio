using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class DriverManager
    {
        private readonly List<IDevice> _devices = new();
        private readonly List<IDriver> _drivers = new();

        public DriverManager()
        {
            
            _drivers.Add(new AsioDriver());
            _drivers.Add(new WasapiDriver());
            // _drivers.Add(new DirectSoundDriver());
            // _drivers.Add(new WaveDriver());

            foreach (var driver in _drivers)
            {
                _devices.AddRange(driver.GetDevices());
            }

            _devices.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _devices.InsertRange(0, new NullDriver().GetDevices());
        }

        public IResourceHandle<IDevice> GetInputDevice(WaveInputDevice inputDevice)
        {
            var virtualDevice = VLApi.Instance.GetInputDevice(inputDevice);
            return virtualDevice;
        }
        public IResourceHandle<IDevice> GetOutputDevice(WaveOutputDevice inputDevice)
        {
            var virtualDevice = VLApi.Instance.GetOutputDevice(inputDevice);
            return virtualDevice;
        }
        
 
        public void AddDriver(IDriver driver)
        {
            _drivers.Add(driver);
            _devices.AddRange(driver.GetDevices());
        }
        
        public IEnumerable<IDevice> GetInputDevices()
        {
            return _devices.Where(d => d.IsInputDevice);
        }

        public IEnumerable<IDevice> GetOutputDevices()
        {
            return _devices.Where(d => d.IsOutputDevice);
        }
    }
}