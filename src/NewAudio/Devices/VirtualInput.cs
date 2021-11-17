using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Nodes;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class VirtualInput : AudioNode, IVirtualDevice
    {
        public override string NodeName => "VirtualInput";
        private readonly IResourceHandle<IAudioClient> _client;
        public IAudioClient AudioClient => _client.Resource;
        public string Name => AudioClient.Name;
        public DeviceConfigRequest ConfigRequest { get; private set; }
        public DeviceConfig Config { get; set; }

        public bool IsPlaying => false;
        public bool IsRecording => true;


        public VirtualInput(IResourceHandle<IAudioClient> client, DeviceConfigRequest request): base(new AudioNodeConfig())
        {
            _client = client;
            AudioClient.Add(this);
            ConfigRequest = request;
        }

        public void Post(AudioDataMessage msg)
        {
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
            return AudioClient?.Name ?? "";
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    AudioClient?.Remove(this);
                    _client?.Dispose();
                    _disposedValue = true;
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}