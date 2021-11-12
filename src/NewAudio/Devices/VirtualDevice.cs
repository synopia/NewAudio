using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public struct VirtualDeviceMapping
    {
        
    }
    public class VirtualDevice : IDisposable
    {
        private IResourceHandle<IDevice> _resource;
        private IDevice _realDevice => _resource?.Resource;

        public IDevice Device => _realDevice;  
        public string Name => _realDevice.Name;
         
        public bool IsInputDevice => _realDevice.IsInputDevice;
        public bool IsOutputDevice => _realDevice.IsOutputDevice;
        public AudioDataProvider AudioDataProvider => _realDevice.AudioDataProvider;
        
        public VirtualDevice(IResourceHandle<IDevice> resource)
        {
            _resource = resource;
        }

        private BroadcastBlock<AudioDataMessage> _broadcastBlock = new BroadcastBlock<AudioDataMessage>(i=>i);
        public async Task<Tuple<DeviceConfigResponse, ISourceBlock<AudioDataMessage>>> CreateInput(DeviceConfigRequest request)
        {
            if (_realDevice == null)
            {
                return null;
            }
            try
            {
                var config= await _realDevice.CreateInput(request, _broadcastBlock);
                return new Tuple<DeviceConfigResponse, ISourceBlock<AudioDataMessage>>(config,_broadcastBlock);
            }
            catch (Exception e)
            {
                Dispose();
                throw;
            }
        }

        private BufferBlock<AudioDataMessage> _bufferBlock = new BufferBlock<AudioDataMessage>();
        public async Task<Tuple<DeviceConfigResponse, ITargetBlock<AudioDataMessage>>> CreateOutput(DeviceConfigRequest request)
        {
            if (_realDevice == null)
            {
                return null;
            }
            try
            {
                
                var config = await _realDevice.CreateOutput(request, _bufferBlock); 
                return new Tuple<DeviceConfigResponse, ITargetBlock<AudioDataMessage>>(config, _bufferBlock);
            }
            catch (Exception e)
            {
                Dispose();
                throw;
            }
        }

        
        public bool Start()
        {
            return _realDevice?.Start() ?? true;
        }

        public bool Stop()
        {
            return _realDevice?.Stop() ?? true;
        }


        public string DebugInfo()
        {
            return "";
        }
        
        private bool _disposedValue;

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _realDevice?.Stop();
                _resource?.Dispose();
                _disposedValue = true;
            }
        }

        public override string ToString()
        {
            return _realDevice?.Name ?? "";
        }
    }
}