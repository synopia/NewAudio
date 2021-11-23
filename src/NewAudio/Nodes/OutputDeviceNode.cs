using System;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Lib.Basics.Resources;
using VL.NewAudio;

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
        public IResourceHandle<DeviceManager> DeviceManager { get; }
        public readonly OutputDeviceParams Params;
        public OutputDeviceBlock OutputDeviceBlock { get; private set; }

        public OutputDeviceNode()
        {
            InitLogger<OutputDeviceNode>();
            DeviceManager = Resources.GetDeviceManager();
            Params = AudioParams.Create<OutputDeviceParams>();
        }

        public bool Update(bool enable, AudioLink input, OutputDeviceSelection deviceSelection, out int maxNumberOfChannels, int channels=2)
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

            if (Params.Input.HasChanged)
            {
                Params.Input.Commit();
                if (AudioBlock != null)
                {
                    AudioBlock.DisconnectAllInputs();
                    Params.Input.Value?.Pin.Connect(AudioBlock);
                }
            }
            if (Params.Enable.HasChanged || (Params.Enable.Value && OutputDeviceBlock is { IsEnabled: false }))
            {
                Params.Enable.Commit();
                OutputDeviceBlock?.SetEnabled(Params.Enable.Value);
            }
            
            if (Params.HasChanged )
            {
                Params.Commit();

                StopDevice();
                if (Params.Device.Value != null)
                {
                    StartDevice();
                }
            }
            maxNumberOfChannels = OutputDeviceBlock?.Device?.MaxNumberOfOutputChannels ?? 0;
            return OutputDeviceBlock?.IsEnabled ?? false;
        }

        public void StartDevice()
        {
            try
            {
                OutputDeviceBlock = DeviceManager.Resource.GetOutputDevice(Params.Device.Value, new AudioBlockFormat()
                {
                    Channels = Params.NumberOfChannels.Value,
                });
            }
            catch (Exception e)
            {
                OutputDeviceBlock?.Dispose();
                OutputDeviceBlock = null;
                AudioBlock = null;
                ExceptionHappened(e, "StartDevice");
            }
            AudioBlock = OutputDeviceBlock;
            Graph.OutputBlock = OutputDeviceBlock;
            Params.Input.Value?.Pin.Connect(AudioBlock);
            if (Params.Enable.Value)
            {
                OutputDeviceBlock.Enable();
            }
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
                    DeviceManager.Dispose();
                }
                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}