using System.Threading.Tasks;

namespace NewAudio.Devices
{
    public class NullAudioClient : BaseAudioClient
    {
        public NullAudioClient(string name, bool isInputDevice, bool isOutputDevice)
        {
            Name = name;
            InitLogger<NullAudioClient>();
            IsInputDevice = isInputDevice;
            IsOutputDevice = isOutputDevice;
        }

        protected override bool Init()
        {
            return true;
        }

        protected override bool Stop()
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