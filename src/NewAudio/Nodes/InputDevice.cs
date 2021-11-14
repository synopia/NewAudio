using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public class InputDeviceParams : AudioParams
    {
        public AudioParam<InputDeviceSelection> Device;
    }

    public class InputDevice : AudioNode
    {
        public override string NodeName => "Input";

        private IResourceHandle<DriverManager> _driverManager;

        public AudioFormat AudioFormat => ActualDeviceParams.AudioFormat;

        public VirtualInput Device { get; private set; }
        public InputDeviceParams Params { get; }
        public ActualDeviceParams ActualDeviceParams { get; private set; }
        public DeviceParams DeviceParams { get; }

        public InputDevice()
        {
            InitLogger<InputDevice>();
            _driverManager = Factory.Instance.GetDriverManager();
            Params = AudioParams.Create<InputDeviceParams>();
            DeviceParams = AudioParams.Create<DeviceParams>();
            Logger.Information("Input device created");
        }

        public AudioLink Update(InputDeviceSelection deviceSelection,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz48000,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            Params.Device.Value = deviceSelection;
            DeviceParams.SamplingFrequency.Value = samplingFrequency;
            DeviceParams.DesiredLatency.Value = desiredLatency;
            DeviceParams.ChannelOffset.Value = channelOffset;
            DeviceParams.Channels.Value = channels;

            if (Params.HasChanged)
            {
                PlayParams.Reset.Value = true;
            }

            return base.Update(Params);
        }

        public override bool Play()
        {
            if (Params.Device.Value != null)
            {
                Device = _driverManager.Resource.GetInputDevice(Params.Device.Value);

                if (Device != null)
                {
                    ActualDeviceParams = Device.Bind(DeviceParams);
                    ActualDeviceParams.Active.OnChange += () =>
                    {
                        Output.Format = ActualDeviceParams.AudioFormat;
                        return Task.CompletedTask;
                    };
                    Device.Start();
                    Output.SourceBlock = Device.SourceBlock;
                    
                    return true;
                }
            }

            return false;
        }

        public override void Stop()
        {
            Device?.Dispose();
            Output.SourceBlock = null;
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
                    Device?.Dispose();
                    Device = null;
                    _driverManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}