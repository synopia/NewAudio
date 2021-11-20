using System;
using System.Runtime.InteropServices;
using NewAudio.Devices;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;

namespace NewAudio.Block
{
    public class VirtualOutput : OutputDeviceBlock
    {
        private IResourceHandle<IDevice> _resourceHandle;
        public override string Name => $"VirtualOutput ({Device?.Name})";
        private byte[] _sampleBuffer;
        public RingBuffer<byte> RingBuffer;
        public VirtualOutput(IResourceHandle<IDevice> resourceHandle, AudioBlockFormat format) : base(resourceHandle.Resource, format)
        {
            _resourceHandle = resourceHandle;
            InitLogger<VirtualOutput>();
            Device.Output = this;
            Graph.OutputDevice = resourceHandle.Resource;
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
        
        protected override void Initialize()
        {
            var deviceFrames = Device.FramesPerBlock;
            bool reconfigureMixingBuffer = false;
            
            Device.Initialize();

            if (NumberOfChannels != Device.NumberOfOutputChannels)
            {
                NumberOfChannels = Device.NumberOfOutputChannels;
                reconfigureMixingBuffer = true;
            }

            if (reconfigureMixingBuffer)
            {
                SetupProcessWithMixing();
            }

            _sampleBuffer = new byte[Device.FramesPerBlock * NumberOfChannels * 4];
            RingBuffer = new RingBuffer<byte>(NumberOfChannels * Device.FramesPerBlock * 4*2);
            // Device.RingBuffer = RingBuffer;
        }

        protected override void Uninitialize()
        {
            Device.Uninitialize();
        }

        protected override void EnableProcessing()
        {
            Device.EnableProcessing();
        }

        protected override void DisableProcessing()
        {
            Device.DisableProcessing();
        }

        protected override bool SupportsProcessInPlace()
        {
            return false;
        }

        public override string ToString()
        {
            return Device?.Name ?? "";
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _resourceHandle.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}