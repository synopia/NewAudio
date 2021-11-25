using System;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Dsp;
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
        private readonly IAudioService _audioService;
        public override string Name { get; }
        private readonly AudioBlockFormat _format;
        private  ulong _lastOverrun;
        private  ulong _lastUnderrun;
        private float _ringBufferPadding = 2;
        public Session Session { get; }
        public DeviceCaps DeviceCaps { get; }

        public AudioBuffer InputBuffer => InternalBuffer;
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

        public InputDeviceBlock(InputDeviceSelection selection, AudioBlockFormat format) : base(format)
        {
            _audioService = Resources.GetAudioService();
            _format = format;
            InitLogger<InputDeviceBlock>();

            DeviceCaps = _audioService.GetDeviceInfo((DeviceSelection)selection.Tag);

            var deviceChannels = DeviceCaps.MaxInputChannels;
            if (ChannelMode != ChannelMode.Specified)
            {
                ChannelMode = ChannelMode.Specified;
                NumberOfChannels = Math.Min(deviceChannels, 2);
            }
            if (deviceChannels < NumberOfChannels)
            {
                NumberOfChannels = deviceChannels;
            }

            Session = _audioService.OpenDevice(((DeviceSelection)selection.Tag).DeviceId, new ChannelConfig{InputChannels = NumberOfChannels});
            var s = $"InputDeviceBlock ({DeviceCaps.Name})";
            Name = s;
            Logger.Information("{Name} created", s);
        }
        protected override bool SupportsProcessInPlace()
        {
            return false;
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
                    SetEnabled(false);
                    _audioService.CloseDevice(Session.SessionId);
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}