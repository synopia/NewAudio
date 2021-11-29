using System;
using System.Threading.Tasks;
using NewAudio.Processor;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Lib.Basics.Resources;

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

        public InputDeviceProcessor InputDeviceProcessor { get; private set; }
        public InputDeviceParams Params { get; }

        public InputDeviceNode()
        {
            
            InitLogger<InputDeviceNode>();
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

            if (Params.Enable.HasChanged && InputDeviceProcessor != null)
            {
                Params.Enable.Commit();
                AudioProcessor.SetEnabled(Params.Enable.Value);
            }

            maxNumberOfChannels = InputDeviceProcessor?.DeviceCaps.MaxInputChannels ?? 0;
            enabled = InputDeviceProcessor?.IsEnabled ?? false;
            
            return Output;
        }

        public void StartDevice()
        {
            InputDeviceProcessor = new InputDeviceProcessor(Params.Device.Value, new AudioProcessorConfig()
                {
                    Channels = Params.NumberOfChannels.Value
                });

            AudioProcessor = InputDeviceProcessor;
        }

        public void StopDevice()
        {
            InputDeviceProcessor?.Dispose();
            InputDeviceProcessor = null;
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
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}