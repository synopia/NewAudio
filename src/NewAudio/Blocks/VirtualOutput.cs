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
        }

        public override unsafe void FillBuffer(IntPtr[] buffers, int numSamples)
        {
            RenderInputs();
            for (int ch = 0; ch < NumberOfChannels; ch++)
            {
                Marshal.Copy(InternalBuffer.Data, ch*FramesPerBlock, buffers[ch], numSamples);
            }
        }

        public unsafe void RenderInputs()
        {
            Graph.PreProcess();
            
            InternalBuffer.Zero();
            PullInputs(InternalBuffer);
            if (CheckNotClipping())
            {
                InternalBuffer.Zero();
            }

            /*fixed (byte* ptr = _sampleBuffer)
            {
                var i = new IntPtr(ptr);
                Marshal.Copy(InternalBuffer.Data, 0, i, Device.FramesPerBlock * NumberOfChannels);
            }

            RingBuffer.Write(_sampleBuffer, Device.FramesPerBlock * NumberOfChannels);*/
            Graph.PostProcess();
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
            Device.Output = this;
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