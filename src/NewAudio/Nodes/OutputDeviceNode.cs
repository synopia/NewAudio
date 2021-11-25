using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;

namespace NewAudio.Nodes
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class OutputDeviceParams : AudioParams
    {
        public AudioParam<bool> Enable;
        public AudioParam<AudioLink> Input;
        public AudioParam<OutputDeviceSelection> Device;
        public AudioParam<int> NumberOfChannels;
    }

    public class OutputDeviceNode : AudioNode
    {
        public override string NodeName => "OutputDevice";
        public readonly OutputDeviceParams Params;
        public OutputDeviceBlock OutputDeviceBlock { get; private set; }

        public OutputDeviceNode()
        {
            InitLogger<OutputDeviceNode>();
            Params = AudioParams.Create<OutputDeviceParams>();
        }

        public bool Update(bool enable, AudioLink input, OutputDeviceSelection deviceSelection,
            out int maxNumberOfChannels, int channels = 2)
        {
            if (InExceptionTimeOut())
            {
                maxNumberOfChannels = -1;
                return false;
            }

            Params.Input.Value = input;
            Params.Enable.Value = enable;

            Params.Device.Value = deviceSelection;
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

            if (Params.Input.HasChanged && AudioBlock != null)
            {
                Params.Input.Commit();
                AudioBlock.DisconnectAllInputs();
                Params.Input.Value?.Pin.Connect(AudioBlock);
            }

            if (Params.Enable.HasChanged && OutputDeviceBlock != null)
            {
                Params.Enable.Commit();
                OutputDeviceBlock.SetEnabled(Params.Enable.Value);
            }

            maxNumberOfChannels = OutputDeviceBlock?.DeviceCaps.MaxOutputChannels ?? 0;
            return OutputDeviceBlock?.IsEnabled ?? false;
        }

        public void StartDevice()
        {
            OutputDeviceBlock = new OutputDeviceBlock(Params.Device.Value, new AudioBlockFormat()
            {
                Channels = Params.NumberOfChannels.Value,
            });

            Params.Input.Value?.Pin.Connect(OutputDeviceBlock);
            Graph.AddOutput(OutputDeviceBlock);
            AudioBlock = OutputDeviceBlock;
        }

        public void StopDevice()
        {
            Graph.Disable();
            OutputDeviceBlock?.Dispose();
            OutputDeviceBlock = null;
        }

        public override string DebugInfo()
        {
            return $"Output device:[{Params.Device.Value}]";
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