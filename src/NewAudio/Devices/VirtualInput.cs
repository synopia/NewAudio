using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Nodes;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class VirtualInput : IVirtualDevice
    {
        private readonly IResourceHandle<IDevice> _resource;
        private IDevice RealDevice => _resource.Resource;

        public IDevice Device => RealDevice;
        public string Name => RealDevice.Name;
        public DeviceParams Params { get; private set; }
        public ActualDeviceParams ActualParams { get; private set; }

        private readonly BufferBlock<AudioDataMessage> _bufferBlock = new(new ExecutionDataflowBlockOptions()
        {
            // todo
            BoundedCapacity = 6
        });
        public bool IsPlaying => false;
        public bool IsRecording => true;

        public ISourceBlock<AudioDataMessage> SourceBlock => _bufferBlock;

        public VirtualInput(IResourceHandle<IDevice> resource)
        {
            _resource = resource;
        }

        public ActualDeviceParams Bind(DeviceParams param)
        {
            Params = param;
            return RealDevice.Add(this);
        }

        public void Post(AudioDataMessage msg)
        {
            _bufferBlock.Post(msg);
        }

        public void Update()
        {
            ActualParams.Commit();
  
        }

        public void Start()
        {
            
            // RealDevice.Start();
        }
        public void Stop()
        {
            // RealDevice.Stop();
        }

        
        public override string ToString()
        {
            return RealDevice?.Name ?? "";
        }

        private bool _disposedValue;

        public void Dispose()
        {
            if (!_disposedValue)
            {
                RealDevice?.Remove(this);
                _resource?.Dispose();
                _disposedValue = true;
            }
        }

    }
}