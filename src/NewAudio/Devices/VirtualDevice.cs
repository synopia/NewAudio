using System.Threading.Tasks;
using NewAudio.Core;

namespace NewAudio.Devices
{
    public struct VirtualDeviceMapping
    {
        
    }
    public class VirtualDevice: IDevice
    {
        private IDevice _realDevice;

        public string Name => _realDevice.Name;
        public bool IsInputDevice => _realDevice.IsInputDevice;
        public bool IsOutputDevice => _realDevice.IsOutputDevice;
        public AudioDataProvider AudioDataProvider => _realDevice.AudioDataProvider;
        
        
        
        public Task<DeviceConfigResponse> Create(DeviceConfigRequest request)
        {
            
        }

        public bool Free()
        {
            throw new System.NotImplementedException();
        }

        public bool Start()
        {
            throw new System.NotImplementedException();
        }

        public bool Stop()
        {
            throw new System.NotImplementedException();
        }
        public void Dispose()
        {
            throw new System.NotImplementedException();
        }
    }
}