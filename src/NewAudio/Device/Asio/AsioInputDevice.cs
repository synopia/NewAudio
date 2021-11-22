using NewAudio.Block;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices.Asio
{
    public class AsioInputDevice: InputDeviceBlock
    {
        public AsioInputDevice(IResourceHandle<IDevice> device, AudioBlockFormat format) : base(device, format)
        {
            InitLogger<AsioInputDevice>();
        }
        
        
    }
}