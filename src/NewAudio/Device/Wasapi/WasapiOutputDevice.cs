using NewAudio.Block;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices.Wasapi
{
    public class WasapiOutputDevice : OutputDeviceBlock
    {
        public WasapiOutputDevice(IResourceHandle<IDevice> device, DeviceBlockFormat format) : base(device, format)
        {
            InitLogger<WasapiOutputDevice>();
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

    }
}