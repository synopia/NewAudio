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
        private bool _allUpToDate;
        public bool IsEnabled { get; private set; } 
        public DeviceManager()
        {
            AudioService = Resources.GetAudioService();
        }

        public int UpdateFormat(AudioParam<SamplingFrequency> samplingFrequency, AudioParam<float> bufferSize)
        {
            var frames = 0;
            foreach (var handle in _devices.ToArray())
            {
                if (handle?.Resource?.IsDisposed ?? true)
                {
                    _devices.Remove(handle);
                    _allUpToDate = false;
                }
            }
            
            if (!_allUpToDate || samplingFrequency.HasChanged || bufferSize.HasChanged )
            {
                samplingFrequency.Commit();
                bufferSize.Commit();
                _allUpToDate = true;
     
                var format = new DeviceFormat()
                {
                    SampleRate = (int)samplingFrequency.Value,
                    BufferSizeMs = bufferSize.Value
                };

                foreach (var device in _devices)
                {
                    device?.Resource?.UpdateFormat(format);
                    frames = Math.Max(frames, device?.Resource?.FramesPerBlock ?? 0);
                }
            }

            return frames;
        }

        public void SetEnabled(bool b)
        {
            if (b)
            {
                Enable();
            }
            else
            {
                Disable();
            }
        }

        public void Enable()
        {
            if (IsEnabled)
            {
                return;
            }

            IsEnabled = true;
            foreach (var handle in _devices)
            {
                handle?.Resource?.EnableProcessing();
            }
        }

        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            IsEnabled = false;
            foreach (var handle in _devices)
            {
                handle?.Resource?.DisableProcessing();
            }
        }
        public InputDeviceBlock GetInputDevice(InputDeviceSelection inputDeviceSelection, AudioBlockFormat format)
        {
            var selection = (DeviceSelection)inputDeviceSelection.Tag;
            Trace.Assert(selection.IsInputDevice);

            var handle = AudioService.OpenDevice(selection.System, selection.Id);
            _devices.Add(handle);
            _allUpToDate = false;
            return new InputDeviceBlock(selection.Name, handle, format);
        }

        public OutputDeviceBlock GetOutputDevice(OutputDeviceSelection outputDeviceSelection, AudioBlockFormat format)
        {
            var selection = (DeviceSelection)outputDeviceSelection.Tag;
            Trace.Assert(selection.IsOutputDevice);

            var handle = AudioService.OpenDevice(selection.System, selection.Id);
            _devices.Add(handle);
            _allUpToDate = false;
            
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