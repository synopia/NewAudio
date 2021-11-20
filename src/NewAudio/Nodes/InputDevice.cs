using System.Threading.Tasks;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.Model;
using Node = VL.MutableModel.Node;

namespace NewAudio.Nodes
{
    public class InputDeviceParams : AudioParams
    {
        public AudioParam<InputDeviceSelection> Device;
    }

    public class InputDevice : AudioNode
    {
        public override string NodeName => "Input";
        private IResourceHandle<DriverManager> _driverManager;
        public VirtualInput Device { get; private set; }
        public InputDeviceParams Params { get; }

        public InputDevice()
        {
            
            InitLogger<InputDevice>();
            _driverManager = Factory.GetDriverManager();
            Params = AudioParams.Create<InputDeviceParams>();
        }
        public AudioLink Update(InputDeviceSelection deviceSelection)
        {
            Params.Device.Value = deviceSelection;

            if (Params.Device.HasChanged)
            {
                StopDevice();
                if (Params.Device.Value != null)
                {
                    StartDevice();
                }
            }
            return Output;
        }

        public void StartDevice()
        {
            Device = _driverManager.Resource.GetInputDevice(Params.Device.Value, new AudioBlockFormat());
            
        }

        public void StopDevice()
        {
            Device?.Dispose();
            Device = null;
        }

        public override string DebugInfo()
        {
            return $"[{this}, {base.DebugInfo()}]";
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    StopDevice();
                    _driverManager.Dispose();
                    Output.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}