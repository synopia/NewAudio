using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewAudio.Block;
using NewAudio.Core;
using VL.Lib.Basics.Resources;
using VL.NewAudio;

namespace NewAudio.Devices
{
    public class DeviceManager : IDisposable
    {
        public IAudioService AudioService { get; } 
        private readonly List<IResourceHandle<Device>> _devices = new();
        public DeviceManager()
        {
            AudioService = Resources.GetAudioService();
        }

        public int UpdateFormat(DeviceFormat deviceFormat)
        {
            var frames = 0;
            foreach (var handle in _devices.ToArray())
            {
                if (handle?.Resource?.IsDisposed ?? true)
                {
                    _devices.Remove(handle);
                }
            }
            foreach (var device in _devices)
            {
                device?.Resource?.UpdateFormat(deviceFormat);
                frames = Math.Max(frames, device?.Resource?.FramesPerBlock ?? 0);
            }

            return frames;
        }
        
        public InputDeviceBlock GetInputDevice(InputDeviceSelection inputDeviceSelection, AudioBlockFormat format)
        {
            var selection = (DeviceSelection)inputDeviceSelection.Tag;
            Trace.Assert(selection.IsInputDevice);

            var handle = AudioService.OpenDevice(selection.System, selection.Id);
            _devices.Add(handle);

            return new InputDeviceBlock(selection.Name, handle, format);
        }

        public OutputDeviceBlock GetOutputDevice(OutputDeviceSelection outputDeviceSelection, AudioBlockFormat format)
        {
            var selection = (DeviceSelection)outputDeviceSelection.Tag;
            Trace.Assert(selection.IsOutputDevice);

            var handle = AudioService.OpenDevice(selection.System, selection.Id);
            _devices.Add(handle);
            
            return new OutputDeviceBlock(selection.Name, handle, format);
        }
        
        public void Dispose()
        {
            // foreach (var handle in _devices)
            // {
                // handle?.Resource?.Dispose();
            // }
        }
    }
}