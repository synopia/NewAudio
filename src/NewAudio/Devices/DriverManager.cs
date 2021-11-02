using System;
using System.Collections.Generic;
using System.Linq;

namespace NewAudio.Devices
{
    public class DriverManager
    {
        private static DriverManager _instance;
        private readonly List<IDevice> _devices = new List<IDevice>();

        private readonly List<IDriver> _drivers = new List<IDriver>();

        public DriverManager()
        {
            _drivers.Add(new AsioDriver());
            _drivers.Add(new DirectSoundDriver());
            _drivers.Add(new NullDriver());
            _drivers.Add(new WasapiDriver());
            _drivers.Add(new WaveDriver());

            foreach (var driver in _drivers) _devices.AddRange(driver.GetDevices());
            _devices.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }

        public static DriverManager Instance
        {
            get
            {
                if (_instance == null) _instance = new DriverManager();

                return _instance;
            }
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