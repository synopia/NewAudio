using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Block;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class DriverManager : IDisposable
    {
        private readonly List<DeviceSelection> _deviceSelections = new();
        private readonly List<IResourceHandle<IDriver>> _drivers = new();

        public DriverManager()
        {
            Init();
        }

        public void Update()
        {
            foreach (var handle in _drivers)
            {
                handle.Resource.Update();
            }
        }
        
        public void Init()
        {
            _deviceSelections.Clear();

            _deviceSelections.AddRange(EnumerateAsio.GetDeviceSelections());
            _deviceSelections.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));
        }

        private IResourceHandle<IDevice> GetHandle(string name, AudioBlockFormat format)
        {
            var selection = _deviceSelections.Find(d => d.ToString() == name);
            if (selection == null)
            {
                return null;
            }

            var provider = ResourceProvider.NewPooledSystemWide(selection.ToString(), s =>
            {
                var driver = selection.Factory(this);
                driver.Initialize();
                return driver;
            }).Finally(RemoveDriver);

            var handle = provider.GetHandle();
            _drivers.Add(handle);
            return handle.Resource.CreateDevice(selection, format);
        }

        public VirtualInput GetInputDevice(InputDeviceSelection inputDeviceSelection, AudioBlockFormat format)
        {
            var device = GetHandle(inputDeviceSelection.Value, format);
            if (device == null)
            {
                return null;
            }

            return new VirtualInput(device, format);
        }

        public VirtualOutput GetOutputDevice(OutputDeviceSelection outputDeviceSelection, AudioBlockFormat format)
        {
            var device = GetHandle(outputDeviceSelection.Value, format);
            if (device == null)
            {
                return null;
            }
            return new VirtualOutput(device, format);
        }

        public IEnumerable<DeviceSelection> GetInputDevices()
        {
            return _deviceSelections.Where(d => d.IsInputDevice);
        }

        public IEnumerable<DeviceSelection> GetOutputDevices()
        {
            return _deviceSelections.Where(d => d.IsOutputDevice);
        }

        
        public void RemoveDriver(IDriver d)
        {
            foreach (var handle in _drivers.ToArray())
            {
                if (handle.Resource == d)
                {
                    _drivers.Remove(handle);
                    handle.Dispose();
                }
            }
            // d?.DisableProcessing();
            // d?.Uninitialize();
        }

        public void Dispose()
        {
            foreach (var handle in _drivers)
            {
                handle?.Dispose();
            }
        }
    }
}