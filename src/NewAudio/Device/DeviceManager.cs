using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Nodes;
using VL.Lib.Basics.Resources;
using VL.NewAudio;
using Xt;

namespace NewAudio.Devices
{
    public class DeviceManager : IDisposable
    {
        public IAudioService AudioService { get; } 
        private readonly List<DeviceSelection> _deviceSelections = new();
        private readonly List<IResourceHandle<IXtDevice>> _device = new();

        public List<IResourceHandle<IXtDevice>> Devices => _device;
        public DeviceManager()
        {
            AudioService = Resources.GetAudioService();
            Init();
        }

        public void Update()
        {
            foreach (var handle in _device)
            {
                // handle.Resource.Update();
            }
        }
        
        public void Init()
        {
            _deviceSelections.Clear();
            var systems = new[] { XtSystem.ASIO, XtSystem.WASAPI, XtSystem.DirectSound };
            foreach (var system in systems)
            {
                using var list = AudioService.GetService(system).OpenDeviceList(XtEnumFlags.Output);

                for (int d = 0; d < list.GetCount(); d++)
                {
                    string id = list.GetId(d);
                    var caps = list.GetCapabilities(id);
                
                    _deviceSelections.Add(new DeviceSelection(system, id, list.GetName(id), (caps & XtDeviceCaps.Input) != 0, (caps & XtDeviceCaps.Output) != 0));                
                }                
            }
            _deviceSelections.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));
            
            OutputDeviceDefinition.Instance.Clear();
            foreach (var selection in GetOutputDevices())
            {
                OutputDeviceDefinition.Instance.AddEntry(selection.ToString(), selection);
            }
            InputDeviceDefinition.Instance.Clear();
            foreach (var selection in GetInputDevices())
            {
                InputDeviceDefinition.Instance.AddEntry(selection.ToString(), selection);
            }
        }

        public InputDeviceBlock GetInputDevice(InputDeviceSelection inputDeviceSelection)
        {
            var selection = (DeviceSelection)inputDeviceSelection.Tag;
            Trace.Assert(selection.IsInputDevice);

            var handle = AudioService.OpenDevice(selection.System, selection.Id);

            Devices.Add(handle);
            var mix = handle.Resource.GetMix();
            var format = new DeviceBlockFormat();
            if (mix != null)
            {
                format.SampleRate = mix.Value.rate;                
            }
            format.Channels = handle.Resource.GetChannelCount(false);

            return new InputDeviceBlock(selection.Name, handle, format);
        }

        public OutputDeviceBlock GetOutputDevice(OutputDeviceSelection outputDeviceSelection, DeviceBlockFormat format)
        {
            var selection = (DeviceSelection)outputDeviceSelection.Tag;
            Trace.Assert(selection.IsOutputDevice);

            var handle = AudioService.OpenDevice(selection.System, selection.Id);
            Devices.Add(handle);

            var device = handle.Resource;
            device.SupportsAccess(true);
            var mix = device.GetMix();
            if (mix != null)
            {
                if (format.SampleRate == 0)
                {
                    format.SampleRate = mix.Value.rate;
                }

                format.DefaultSample = mix.Value.sample;
            }

            if (format.Channels == 0)
            {
                format.Channels = device.GetChannelCount(true);
            }

            try
            {
                return new OutputDeviceBlock(selection.Name, handle, format);
            }
            catch (Exception e)
            {
                handle.Dispose();
                throw;
            }
        }

        public IEnumerable<DeviceSelection> GetInputDevices()
        {
            return _deviceSelections.Where(d => d.IsInputDevice);
        }

        public IEnumerable<DeviceSelection> GetOutputDevices()
        {
            return _deviceSelections.Where(d => d.IsOutputDevice);
        }

        
        public void RemoveDevice(IXtDevice d)
        {
            foreach (var handle in _device.ToArray())
            {
                if (handle.Resource == d)
                {
                    _device.Remove(handle);
                    handle.Dispose();
                }
            }
            // d?.DisableProcessing();
            // d?.Uninitialize();
        }

        public void Dispose()
        {
            foreach (var handle in _device)
            {
                handle?.Dispose();
            }
        }
    }
}