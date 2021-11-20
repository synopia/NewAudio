using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Lib.Basics.Resources;

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
    }
    public class OutputDeviceNode : AudioNode
    {
        public override string NodeName => "OutputDevice";
        private readonly IResourceHandle<DeviceManager> _driverManager;
        public readonly OutputDeviceParams Params;
        public OutputDeviceBlock Device { get; private set; }

        private int _counter;
        private long _lag;
        public double LagMs { get; private set; }
        
        public OutputDeviceNode()
        {
            InitLogger<OutputDeviceNode>();
            _driverManager = Factory.GetDriverManager();
            Params = AudioParams.Create<OutputDeviceParams>();
        }

        public bool Update(bool enable, AudioLink input, OutputDeviceSelection deviceSelection, int channelOffset, int channels)
        {
            Params.Input.Value = input;
            Params.Device.Value = deviceSelection;
            Params.Enable.Value = enable;
            Params.ChannelOffset.Value = channelOffset;
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

            if (Params.ChannelOffset.HasChanged || Params.NumberOfChannels.HasChanged)
            {
                // Device.NumberOfChannels = Params.NumberOfChannels.Value;
                // Device.ChannelOffset = Params.ChannelOffset.Value;
            }
            if (Params.HasChanged)
            {
                Params.Commit();

                Params.Input.Value?.Pin.Connect(AudioBlock);
                Graph.OutputBlock = Device;

                Device?.SetEnabled(Params.Enable.Value);
            }

            return Device?.IsEnabled ?? false;
        }

        public void StartDevice()
        {
            Device = _driverManager.Resource.GetOutputDevice(Params.Device.Value, new DeviceBlockFormat()
            {
                ChannelOffset = Params.ChannelOffset.Value,
                Channels = Params.NumberOfChannels.Value,
                ChannelMode = ChannelMode.Specified
            });
            AudioBlock = Device;
        }

        public void StopDevice()
        {
            Graph.Disable();
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
                    _driverManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}