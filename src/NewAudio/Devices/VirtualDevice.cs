using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class VirtualDevice : IDisposable
    {
        private readonly IResourceHandle<IDevice> _resource;
        private IDevice RealDevice => _resource?.Resource;

        public IDevice Device => RealDevice;
        public string Name => RealDevice.Name;

        public bool IsInputDevice => RealDevice.IsInputDevice;
        public bool IsOutputDevice => RealDevice.IsOutputDevice;
        public AudioDataProvider AudioDataProvider => RealDevice.AudioDataProvider;
        private readonly BufferBlock<AudioDataMessage> _bufferBlock = new();

        public VirtualDevice(IResourceHandle<IDevice> resource)
        {
            _resource = resource;
        }

        private BroadcastBlock<AudioDataMessage> _broadcastBlock = new(i => i);

        public async Task<Tuple<DeviceConfigResponse, ISourceBlock<AudioDataMessage>>> CreateInput(
            DeviceConfigRequest request)
        {
            if (RealDevice == null)
            {
                return null;
            }

            try
            {
                var config = await RealDevice.CreateInput(request, _broadcastBlock);
                return new Tuple<DeviceConfigResponse, ISourceBlock<AudioDataMessage>>(config, _broadcastBlock);
            }
            catch (Exception _)
            {
                // todo is this necessary?
                Dispose();
                throw;
            }
        }


        public async Task<Tuple<DeviceConfigResponse, ITargetBlock<AudioDataMessage>>> CreateOutput(
            DeviceConfigRequest request)
        {
            if (RealDevice == null)
            {
                return null;
            }

            try
            {
                var config = await RealDevice.CreateOutput(request, _bufferBlock);
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
            return RealDevice?.Start() ?? true;
        }

        public bool Stop()
        {
            return RealDevice?.Stop() ?? true;
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
                RealDevice?.Stop();
                _resource?.Dispose();
                _disposedValue = true;
            }
        }

        public override string ToString()
        {
            return RealDevice?.Name ?? "";
        }
    }
}