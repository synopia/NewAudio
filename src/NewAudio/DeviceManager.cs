using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using NewAudio.Internal;
using Serilog;
using Stride.Core;

namespace NewAudio
{
    public interface IDevice : IDisposable
    {
        public bool IsPlaying { get; }
        public void Start();
        public void Stop();
        public AudioFormat Format { get; }
    }

    public class DeviceManager 
    {
        private readonly ILogger _logger = Log.ForContext<DeviceManager>();
        
        private readonly Dictionary<WaveOutputDevice, OutputDevice> _outputDevices = new Dictionary<WaveOutputDevice, OutputDevice>();
        private readonly Dictionary<WaveInputDevice, InputDevice> _inputDevices = new Dictionary<WaveInputDevice, InputDevice>();
        public AudioFormat Format {get; private set; }

        public DeviceManager()
        {
            Format = new AudioFormat(0, 48000, 256);
        }

        public InputDevice GetInputDevice(WaveInputDevice deviceHandle)
        {
            if (_inputDevices.ContainsKey(deviceHandle))
            {
                var inputDevice = _inputDevices[deviceHandle];
                inputDevice.IncreaseRef();
                return inputDevice;
            }

            var device = new InputDevice(deviceHandle, Format.WithChannels(2));
            device.IncreaseRef();
            _inputDevices[deviceHandle] = device;

            return device;
        }
        
        public OutputDevice GetOutputDevice(WaveOutputDevice deviceHandle)
        {
            if (_outputDevices.ContainsKey(deviceHandle))
            {
                var outputDevice = _outputDevices[deviceHandle];
                outputDevice.IncreaseRef();
                return outputDevice;
            }

            var device = new OutputDevice(deviceHandle, Format.WithChannels(2));
            device.IncreaseRef();
            _outputDevices[deviceHandle] = device;
            return device;
        }

        public void ReleaseInputDevice(InputDevice device)
        {
            device.Stop();
            if (device.DecreaseRef())
            {
                _inputDevices.Remove(device.Handle);
            }
            device.Dispose();
        }
        public void ReleaseOutputDevice(OutputDevice device)
        {
            device.Stop();
            if (device.DecreaseRef())
            {
                _outputDevices.Remove(device.Handle);
            }            
            device.Dispose();
        }

        public void Start()
        {
            _logger.Information("Starting all active devices");
            foreach (var inputDevice in _inputDevices.Values)
            {
                inputDevice.Start();
            }

            foreach (var outputDevice in _outputDevices.Values)
            {
                outputDevice.Start();
            }
        }

        public void Stop()
        {
            _logger.Information("Stopping all active devices");
            foreach (var inputDevice in _inputDevices.Values)
            {
                inputDevice.Stop();
            }

            foreach (var outputDevice in _outputDevices.Values)
            {
                outputDevice.Stop();
            }
        }

        public void Dispose()
        {
            Stop();
            foreach (var inputDevice in _inputDevices.Values)
            {
                inputDevice.Dispose();
            }

            foreach (var outputDevice in _outputDevices.Values)
            {
                outputDevice.Dispose();
            }
            _inputDevices.Clear();
            _outputDevices.Clear();
            
        }
    }
}