using System;
using NewAudio.Core;
using VL.Lib.Basics.Resources;

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


    public class InputDeviceBlock : InputBlock
    {
        public override string Name { get; }
        private IResourceHandle<IXtDevice> _device;
        protected IXtDevice Device => _device.Resource;
        private readonly AudioBlockFormat _format;
        private  ulong _lastOverrun;
        private  ulong _lastUnderrun;
        private float _ringBufferPadding = 2;
        public int InputChannelOffset { get; set; }

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

        public InputDeviceBlock(string name, IResourceHandle<IXtDevice> device, AudioBlockFormat format) : base(format)
        {
            var s = $"InputDeviceBlock ({name})";
            Name = s;
            _device = device;
            _format = format;
            InitLogger<InputDeviceBlock>();
            Logger.Information("{Name} created", s);

            var deviceChannels = _format.Channels;
            if (ChannelMode != ChannelMode.Specified)
            {
                ChannelMode = ChannelMode.Specified;
                NumberOfChannels = Math.Min(deviceChannels, 2);
            }
            if (deviceChannels < NumberOfChannels)
            {
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
        private bool _disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _device.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}