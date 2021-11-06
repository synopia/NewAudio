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

        public override Task<DeviceConfigResponse> Create(DeviceConfigRequest config)
        {
            return Task.FromResult(new DeviceConfigResponse());
        }

        public override Task<bool> Free()
        {
            return Task.FromResult(true);
        }

        public override bool Start()
        {
            return true;
        }

        public override bool Stop()
        {
            return true;
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