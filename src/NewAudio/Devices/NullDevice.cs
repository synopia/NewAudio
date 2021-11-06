using System.Threading.Tasks;
using NAudio.Wave;
using SharedMemory;

namespace NewAudio.Devices
{
    public class NullDevice : BaseDevice
    {
        public NullDevice(string name, bool isInputDevice, bool isOutputDevice)
        {
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = isOutputDevice;
        }

        public override Task<DeviceConfigResponse> CreateResources(DeviceConfigRequest config)
        {
            return Task.FromResult(new DeviceConfigResponse());
        }

        public override Task<bool> FreeResources()
        {
            return Task.FromResult(true);
        }

        public override Task<bool> StartProcessing()
        {
            return Task.FromResult(true);
        }

        public override Task<bool> StopProcessing()
        {
            return Task.FromResult(true);
        }

        private bool _disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = disposing;
            }
            base.Dispose(disposing);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}