using NewAudio.Block;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public class AsioOutputDevice: OutputDeviceBlock
    {
        public AsioOutputDevice(IResourceHandle<IDevice> device, DeviceBlockFormat format) : base(device, format)
        {
            InitLogger<AsioOutputDevice>();
            Graph.OutputBlock = this;
        }

        protected override void Initialize()
        {
            var deviceFrames = Device.FramesPerBlock;
            bool reconfigureMixingBuffer = false;
            
            Device.Initialize();

            if (NumberOfChannels > Device.MaxNumberOfOutputChannels)
            {
                NumberOfChannels = Device.MaxNumberOfOutputChannels;
                reconfigureMixingBuffer = true;
            }

            if (reconfigureMixingBuffer)
            {
                SetupProcessWithMixing();
            }

            // _sampleBuffer = new byte[Device.FramesPerBlock * NumberOfChannels * 4];
            // RingBuffer = new RingBuffer<byte>(NumberOfChannels * Device.FramesPerBlock * 4*2);
            // Device.RingBuffer = RingBuffer;
        }

        protected override void EnableProcessing()
        {
            Device.EnableProcessing();
        }

        protected override void DisableProcessing()
        {
        }

        protected override void Uninitialize()
        {
        }

        public override AudioBuffer RenderInputs()
        {
            Graph.PreProcess();
            
            InternalBuffer.Zero();
            PullInputs(InternalBuffer);
            if (CheckNotClipping())
            {
                InternalBuffer.Zero();
            }
          
            Graph.PostProcess();

            return InternalBuffer;
        }
    }
}