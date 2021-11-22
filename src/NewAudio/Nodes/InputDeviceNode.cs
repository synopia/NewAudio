using System.Threading.Tasks;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.Model;
using VL.NewAudio;
using Node = VL.MutableModel.Node;

namespace NewAudio.Nodes
{
    public class InputDeviceParams : AudioParams
    {
        public AudioParam<InputDeviceSelection> Device;
    }

    public class InputDeviceNode : AudioNode
    {
        public override string NodeName => "Input";
        public IResourceHandle<DeviceManager> DeviceManager { get; }

        public InputDeviceBlock Device { get; private set; }
        public InputDeviceParams Params { get; }

        public InputDeviceNode()
        {
            
            InitLogger<InputDeviceNode>();
            DeviceManager = Resources.GetDeviceManager();
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
            Device = DeviceManager.Resource.GetInputDevice(Params.Device.Value);
            
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
                    Output.Dispose();
                    DeviceManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}