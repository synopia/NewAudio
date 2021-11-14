using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public class InputDeviceInitParams : AudioNodeInitParams
    {
        public AudioParam<InputDeviceSelection> Device;
    }

    public class InputDevicePlayParams : AudioNodePlayParams
    {
    }

    public class InputDevice : AudioNode<InputDeviceInitParams, InputDevicePlayParams>
    {
        public override string NodeName => "Input";

        private IResourceHandle<DriverManager> _driverManager;

        public AudioFormat AudioFormat => ActualDeviceParams.AudioFormat;

        public VirtualInput Device { get; private set; }
        public ActualDeviceParams ActualDeviceParams { get; private set; }
        public DeviceParams DeviceParams { get; private set; }

        public InputDevice()
        {
            InitLogger<InputDevice>();
            _driverManager = Factory.Instance.GetDriverManager();
            DeviceParams = AudioParams.Create<DeviceParams>();
            Logger.Information("Input device created");
        }

        public AudioLink Update(InputDeviceSelection deviceSelection,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz48000,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            InitParams.Device.Value = deviceSelection;
            DeviceParams.SamplingFrequency.Value = samplingFrequency;
            DeviceParams.DesiredLatency.Value = desiredLatency;
            DeviceParams.ChannelOffset.Value = channelOffset;
            DeviceParams.Channels.Value = channels;
            ActualDeviceParams?.Update();
            
            return base.Update();
        }

        public override bool IsInitValid()
        {
            return InitParams.Device.Value != null;
        }

        public override Task<bool> Init()
        {
            if (InitParams.Device.Value == null)
            {
                Logger.Error("No input device selected. Should not happen!");
            }

            Device = _driverManager.Resource.GetInputDevice(InitParams.Device.Value);

            if (Device == null)
            {
                return Task.FromResult(false);
            }

            ActualDeviceParams = Device.Bind(DeviceParams);
            ActualDeviceParams.Active.OnChange += () =>
            {
                Output.Format = ActualDeviceParams.AudioFormat;
                return Task.CompletedTask;
            };
            /*
            var req = new DeviceConfigRequest
            {
                Latency = InitParams.DesiredLatency.Value,
                AudioFormat = new AudioFormat((int)InitParams.SamplingFrequency.Value, 512, InitParams.Channels.Value),
                Channels = InitParams.Channels.Value,
                ChannelOffset = InitParams.ChannelOffset.Value
            };
            var res = await Device.CreateInput(req);
            if (res == null)
            {
                return false;
            }

            var resp = res.Item1;
            Logger.Information(
                "Input device changed: {Device} Channels={Channels}, Driver Channels={Driver}, Latency={Latency}, Frame size={FrameSize}",
                Device, resp.Channels, resp.DriverChannels, resp.Latency, resp.FrameSize);

            AudioFormat = resp.AudioFormat;
            Output.SourceBlock = res.Item2;
            Output.Format = AudioFormat;
            */

            return Task.FromResult(true);
        }

        public override Task<bool> Free()
        {
            Device?.Dispose();
            Output.SourceBlock = null;
            return Task.FromResult(true);
        }

        public override bool Play()
        {
            Device.Start();
            Output.SourceBlock = Device.SourceBlock;
            return true;
        }

        public override bool Stop()
        {
            Device.Stop();
            return true;
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