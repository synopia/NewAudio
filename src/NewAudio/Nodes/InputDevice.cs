using System;
using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public abstract class InputNode : AudioNode
    {
        protected InputNode(AudioNodeConfig config) : base(config)
        {
            if (ChannelMode != ChannelMode.Specified)
            {
                ChannelMode = ChannelMode.FromOutput;
            }

            if (!config.IsAutoEnableSet)
            {
                AutoEnabled = false;
            }
        }
        public abstract void UpdateConfig(DeviceConfig config);
        public abstract void OnDataReceived(byte[] buffer);

        protected override void ConnectInput(AudioNode input)
        {
            throw new InvalidOperationException("Not supported!");

        }
    }
    public class InputDeviceParams : AudioParams
    {
        public AudioParam<InputDeviceSelection> Device;
        public AudioParam<int> ChannelOffset;
        public AudioParam<int> NumberOfChannels;
        public AudioParam<int> DesiredLatency;
    }

    public class InputDevice : AudioNode
    {
        public override string NodeName => "Input";

        private IResourceHandle<DriverManager> _driverManager;

        public VirtualInput Device { get; private set; }
        public InputDeviceParams Params { get; }
        public InputDevice(AudioNodeConfig config) : base(config)
        {
            InitLogger<InputDevice>();
            _driverManager = Factory.GetDriverManager();
            Params = AudioParams.Create<InputDeviceParams>();
            Logger.Information("Input device created");
        }

        public AudioLink Update(InputDeviceSelection deviceSelection,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz48000,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            Params.Device.Value = deviceSelection;
            Params.DesiredLatency.Value = desiredLatency;
            Params.ChannelOffset.Value = channelOffset;
            Params.NumberOfChannels.Value = channels;

            if (Params.HasChanged)
            {
                StopDevice();
                StartDevice();
                Params.Commit();
            }

            return Output;
        }

        public void StartDevice()
        {
            if (Params.Device.Value == null || Params.NumberOfChannels.Value<=0 )
            {
                return;
            }
            Device = _driverManager.Resource.GetInputDevice(Params.Device.Value, new DeviceConfigRequest()
            {
                Channels = Params.NumberOfChannels.Value,
                ChannelOffset = Params.ChannelOffset.Value,
                DesiredLatency = Params.DesiredLatency.Value,
            });

            if (Device == null)
            {
                return;
            }

            Device.Connect(this);
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
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}