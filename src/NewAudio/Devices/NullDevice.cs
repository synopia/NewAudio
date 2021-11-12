using System.Threading.Tasks;

namespace NewAudio.Devices
{
    public class NullDevice : BaseDevice
    {
        public NullDevice(string name, bool isInputDevice, bool isOutputDevice)
        {
            Name = name;
            InitLogger<NullDevice>();
            IsInputDevice = isInputDevice;
            IsOutputDevice = isOutputDevice;
        }

        protected override Task<bool> Init()
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