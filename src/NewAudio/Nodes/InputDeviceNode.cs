using System;
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
        public AudioParam<bool> Enable;
        public AudioParam<InputDeviceSelection> Device;
        public AudioParam<int> NumberOfChannels;
    }

    public class InputDeviceNode : AudioNode
    {
        public override string NodeName => "Input";
        public IResourceHandle<DeviceManager> DeviceManager { get; }

        public InputDeviceBlock InputDeviceBlock { get; private set; }
        public InputDeviceParams Params { get; }

        public InputDeviceNode()
        {
            
            InitLogger<InputDeviceNode>();
            DeviceManager = Resources.GetDeviceManager();
            Params = AudioParams.Create<InputDeviceParams>();
        }
        public AudioLink Update(bool enable, InputDeviceSelection deviceSelection, out int maxNumberOfChannels, out bool enabled, int channels=2)
        {
            if (InExceptionTimeOut())
            {
                maxNumberOfChannels = -1;
                enabled = false;
                return Output;
            }
            Params.Device.Value = deviceSelection;
            Params.Enable.Value = enable;
            Params.NumberOfChannels.Value = channels;

            if (Params.Device.HasChanged)
            {
                Params.Device.Commit();
                StopDevice();
                if (Params.Device.Value != null)
                {
                    StartDevice();
                }
            }

            if (Params.Enable.HasChanged && InputDeviceBlock != null)
            {
                Params.Enable.Commit();
                AudioBlock.SetEnabled(Params.Enable.Value);
            }

            maxNumberOfChannels = InputDeviceBlock?.Device?.MaxNumberOfInputChannels ?? 0;
            enabled = InputDeviceBlock?.IsEnabled ?? false;
            
            return Output;
        }

        public void StartDevice()
        {
            try
            {
                InputDeviceBlock = DeviceManager.Resource.GetInputDevice(Params.Device.Value, new AudioBlockFormat()
                {
                    Channels = Params.NumberOfChannels.Value
                });
            }
            catch (Exception e)
            {
                InputDeviceBlock?.Dispose();
                InputDeviceBlock = null;
                AudioBlock = null;
                ExceptionHappened(e, "StartDevice");
            }

            InputDeviceBlock.IsAutoEnable = Params.Enable.Value;
            AudioBlock = InputDeviceBlock;
        }

        public void StopDevice()
        {
            InputDeviceBlock?.Dispose();
            InputDeviceBlock = null;
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