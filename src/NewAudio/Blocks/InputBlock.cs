using System;
using NewAudio.Core;
using NewAudio.Devices;

namespace NewAudio.Block
{
    public abstract class InputBlock : AudioBlock
    {
        protected InputBlock(AudioBlockFormat format) : base(format)
        {
            if (ChannelMode != ChannelMode.Specified)
            {
                ChannelMode = ChannelMode.MatchesOutput;
            }

            if (!format.IsAutoEnableSet)
            {
                IsAutoEnable = false;
            }
        }

        protected override void ConnectInput(AudioBlock input)
        {
            throw new InvalidOperationException("Not supported!");

        }
    }


    public abstract class InputDeviceBlock : InputBlock
    {
        public override string Name => $"InputDeviceBlock ({Device?.Name})";
        protected IDevice Device { get; set; }
        private  ulong _lastOverrun;
        private  ulong _lastUnderrun;
        private float _ringBufferPadding = 2;

        public float RingBufferPadding
        {
            get => _ringBufferPadding;
            set => _ringBufferPadding = Math.Max(1, value);
        }

        public ulong LastOverrun
        {
            get
            {
                var value = _lastOverrun;
                _lastOverrun = 0;
                return value;
            }
        }

        public ulong LastUnderrun
        {
            get
            {
                var value = _lastUnderrun;
                _lastUnderrun = 0;
                return value;
            }
        }

        protected InputDeviceBlock(IDevice device, AudioBlockFormat format) : base(format)
        {
            Device = device;
            
            var deviceChannels = Device.NumberOfInputChannels;
            if (ChannelMode != ChannelMode.Specified)
            {
                ChannelMode = ChannelMode.Specified;
                NumberOfChannels = Math.Min(deviceChannels, 2);
            }
            if (deviceChannels < NumberOfChannels)
            {
                Logger.Error("Cannot aquire {Channel} channels! Max: {DeviceChannels}", NumberOfChannels, deviceChannels);
                NumberOfChannels = deviceChannels;
            }
        }
        
        protected void MarkUnderrun()
        {
            _lastUnderrun = Graph.NumberOfProcessedFrames;
        }

        protected void MarkOverrun()
        {
            _lastOverrun = Graph.NumberOfProcessedFrames;
        }
        
    }
}