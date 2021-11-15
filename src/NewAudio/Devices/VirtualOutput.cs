using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Nodes;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class VirtualOutput : IVirtualDevice
    {
        private readonly IResourceHandle<IDevice> _resource;
        private IDevice RealDevice => _resource.Resource;

        public IDevice Device => RealDevice;
        public string Name => RealDevice.Name;
        public DeviceParams Params { get; private set; }
        public ActualDeviceParams ActualParams { get; private set; }
        public bool IsPlaying => true;
        public bool IsRecording => false;

        // private BroadcastBlock<AudioDataMessage> _broadcastBlock = new(i => i, new DataflowBlockOptions()
        // {
            // todo
            // BoundedCapacity = 6
            
        // });

        private ActionBlock<AudioDataMessage> _actionBlock;
        public ITargetBlock<AudioDataMessage> TargetBlock => _actionBlock;
        public VirtualOutput(IResourceHandle<IDevice> resource)
        {
            _resource = resource;
        }

        public void Post(AudioDataMessage msg)
        {
            throw new NotImplementedException();
        }

        public ActualDeviceParams Bind(DeviceParams param)
        {
            Params = param;
            ActualParams = RealDevice.Add(this);
            _actionBlock = new ActionBlock<AudioDataMessage>(msg =>
            {
                Update();
                if (msg.Channels != ActualParams.Channels.Value)
                {
                    return;
                }
                var buffer = RealDevice.GetMixBuffer();
                buffer.WriteChannelsInterleaved(ActualParams.FirstChannel, msg.Channels, msg.Data);
            }, new ExecutionDataflowBlockOptions()
            {
                BoundedCapacity = 1,
                MaxDegreeOfParallelism = 1
            });
            return ActualParams;
        }

        public void Update()
        {
            ActualParams.Commit();
        }

        public void Start()
        {
        }
        public void Stop()
        {
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