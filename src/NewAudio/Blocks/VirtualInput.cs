using NewAudio.Devices;
using VL.Lib.Basics.Resources;

namespace NewAudio.Block
{
    public class VirtualInput : InputDeviceBlock
    {
        private IResourceHandle<IDevice> _resourceHandle;
        public override string Name => $"VirtualInput ({Device?.Name})";

        public VirtualInput(IResourceHandle<IDevice> resourceHandle, AudioBlockFormat format) : base(resourceHandle.Resource, format)
        {
            _resourceHandle = resourceHandle;
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _resourceHandle.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

    }
}