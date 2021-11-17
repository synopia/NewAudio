using System;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Nodes;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class VirtualOutput :AudioNode, IVirtualDevice
    {
        public override string NodeName => "VirtualOutput";
        
        private readonly IResourceHandle<IAudioClient> _client;
        public IAudioClient AudioClient => _client.Resource;
        public string Name => AudioClient.Name;
        public DeviceConfigRequest ConfigRequest { get; private set; }
        public DeviceConfig Config { get; set; }
        public bool IsPlaying => true;
        public bool IsRecording => false;

        private bool _wasEnabledBeforeDeviceChange;


        public VirtualOutput(IResourceHandle<IAudioClient> client, DeviceConfigRequest request): base(new AudioNodeConfig())
        {
            _client = client;
            AudioClient.Add(this);
            ConfigRequest = request;
        }

        public void UpdateConfig(DeviceConfig config)
        {
            Config = config;
        }

        public void Post(AudioDataMessage msg)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            Graph.InitializeAllNodes();
            Graph.SetEnabled(_wasEnabledBeforeDeviceChange);

            // RealDevice.UnPause(this);
            // IsSilent = false;
        }
        public void Stop()
        {
            _wasEnabledBeforeDeviceChange = IsEnabled;
            Graph.Disable();
            Graph.UnInitializeAllNodes();
            
            // RealDevice.Pause(this);
            // IsSilent = true;
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