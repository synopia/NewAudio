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
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> NumberOfChannels;
        public AudioParam<SamplingFrequency> SampleFrequency;
        public AudioParam<float> BufferSize;
    }

    public class OutputDeviceNode : AudioNode
    {
        public override string NodeName => "OutputDevice";
        public IResourceHandle<DeviceManager> DeviceManager { get; }
        public readonly OutputDeviceParams Params;
        public OutputDeviceBlock Device { get; private set; }

        public OutputDeviceNode()
        {
            InitLogger<OutputDeviceNode>();
            DeviceManager = Resources.GetDeviceManager();
            Params = AudioParams.Create<OutputDeviceParams>();
        }

        public bool Update(bool enable, AudioLink input, OutputDeviceSelection deviceSelection, int channelOffset,
            int channels, out int maxNumberOfChannels, SamplingFrequency samplingFrequency=SamplingFrequency.Hz48000, float bufferSize=10)
        {
            Params.Input.Value = input;
            Params.Enable.Value = enable;

            Params.Device.Value = deviceSelection;
            Params.ChannelOffset.Value = channelOffset;
            Params.NumberOfChannels.Value = channels;
            Params.SampleFrequency.Value = samplingFrequency;
            Params.BufferSize.Value = bufferSize;

            if (Params.Input.HasChanged)
            {
                Params.Input.Commit();
                if (AudioBlock != null)
                {
                    AudioBlock.DisconnectAllInputs();
                    Params.Input.Value?.Pin.Connect(AudioBlock);
                }
            }
            if (Params.Enable.HasChanged || (Params.Enable.Value && Device is { IsEnabled: false }))
            {
                Params.Enable.Commit();
                Device?.SetEnabled(Params.Enable.Value);
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
            maxNumberOfChannels = Device?.MaxNumberOfChannels ?? 0;
            
            return Device?.IsEnabled ?? false;
        }

        public void StartDevice()
        {
            try
            {
                Device = DeviceManager.Resource.GetOutputDevice(Params.Device.Value, new DeviceBlockFormat()
                {
                    Channels = Params.NumberOfChannels.Value,
                    ChannelOffset = Params.ChannelOffset.Value,
                    SampleRate = (int)Params.SampleFrequency.Value,
                    BufferSize = Params.BufferSize.Value
                });
            }
            catch (Exception e)
            {
                Device?.Dispose();
                Device = null;
                AudioBlock = null;
                ExceptionHappened(e, "StartDevice");
            }
            AudioBlock = Device;
            Graph.OutputBlock = Device;
            Params.Input.Value?.Pin.Connect(AudioBlock);
        }

        public void StopDevice()
        {
            Graph.Disable();
            Graph.OutputBlock = null;
            Device?.Dispose();
            Device = null;
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