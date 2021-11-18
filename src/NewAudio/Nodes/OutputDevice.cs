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
        public AudioParam<AudioLink> Input;
        public AudioParam<OutputDeviceSelection> Device;
    }
    public class OutputDevice : AudioNode
    {
        public override string NodeName => "OutputDevice";
        private readonly IResourceHandle<DeviceManager> _driverManager;
        public readonly OutputDeviceParams Params;
        public VirtualOutput Device { get; private set; }

        private int _counter;
        private long _lag;
        public double LagMs { get; private set; }
        
        public OutputDevice()
        {
            InitLogger<OutputDevice>();
            _driverManager = Factory.GetDriverManager();
            Params = AudioParams.Create<OutputDeviceParams>();
            Logger.Information("Output device created");
        }

        public void Update(AudioLink input, OutputDeviceSelection deviceSelection)
        {
            Params.Input.Value = input;
            Params.Device.Value = deviceSelection;

            
            if (Params.Device.HasChanged)
            {
                StopDevice();
                if (Params.Device.Value != null)
                {
                    StartDevice();
                }
            }
            if (Params.Input.HasChanged)
            {
                Params.Input.Value.AudioBlock.Connect(Output.AudioBlock);
            }
        }

        public void StartDevice()
        {
            Device = _driverManager.Resource.GetOutputDevice(Params.Device.Value, new AudioBlockFormat()
            {
                Channels = 2,
                ChannelMode = ChannelMode.Specified
            });
            Output.AudioBlock = Device;
            Graph.OutputBlock = Device;
        }

        public void StopDevice()
        {
            Graph.Disable();
            Device?.Dispose();
            Device = null;
        }
        
        public override string DebugInfo()
        {
            return $"Output device:[{base.DebugInfo()}]";
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