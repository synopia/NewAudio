using NewAudio.Processor;
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
        public OutputDeviceProcessor OutputDeviceProcessor { get; private set; }

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

            if (Params.Input.HasChanged && AudioProcessor != null)
            {
                Params.Input.Commit();
                AudioProcessor.DisconnectAllInputs();
                Params.Input.Value?.Pin.Connect(AudioProcessor);
            }

            if (Params.Enable.HasChanged && OutputDeviceProcessor != null)
            {
                Params.Enable.Commit();
                OutputDeviceProcessor.SetEnabled(Params.Enable.Value);
            }

            maxNumberOfChannels = OutputDeviceProcessor?.DeviceCaps.MaxOutputChannels ?? 0;
            return OutputDeviceProcessor?.IsEnabled ?? false;
        }

        public void StartDevice()
        {
            OutputDeviceProcessor = new OutputDeviceProcessor(Params.Device.Value, new AudioProcessorConfig()
            {
                Channels = Params.NumberOfChannels.Value,
            });

            Params.Input.Value?.Pin.Connect(OutputDeviceProcessor);
            Graph.AddOutput(OutputDeviceProcessor);
            AudioProcessor = OutputDeviceProcessor;
        }

        public void StopDevice()
        {
            Graph.Disable();
            OutputDeviceProcessor?.Dispose();
            OutputDeviceProcessor = null;
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