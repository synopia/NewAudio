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
            _driverManager = Factory.GetDriverManager();
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

            if (Params.Device.HasChanged)
            {
                StopDevice();
                if (Params.Device.Value != null)
                {
                    StartDevice();
                }
            }
            
            PlayParams.Update(null, Params.HasChanged);

            return base.Update(Params);
        }

        public void StartDevice()
        {
            Device = _driverManager.Resource.GetInputDevice(Params.Device.Value);

            if (Device == null)
            {
                return;
            }

            ActualDeviceParams = Device.Bind(DeviceParams);
            ActualDeviceParams.Active.OnChange += () =>
            {
                Output.Format = ActualDeviceParams.AudioFormat;
                return Task.CompletedTask;
            };
            Output.SourceBlock = Device.SourceBlock;
        }

        public void StopDevice()
        {
            Device?.Dispose();
            Device = null;
        }

        public override bool Play()
        {
            if (Device != null)
            {
                Device.Start();
                return true;
            }

            return false;
        }

        public override void Stop()
        {
            Device?.Stop();
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