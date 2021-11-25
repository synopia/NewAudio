using System;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;
using Xt;

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

    public delegate void InputProcessor(AudioBuffer buffer, int sampleRate, int frames);
    public class InputProcessorBlock : InputBlock
    {
        public override string Name => "Input Processor";
        private InputProcessor _inputProcessor;
        
        public InputProcessorBlock(InputProcessor processor, AudioBlockFormat format) : base(format)
        {
            InitLogger<InputProcessorBlock>();
            _inputProcessor = processor;
        }

        protected override void Process(AudioBuffer buffer, int numFrames)
        {
            _inputProcessor.Invoke(buffer, SampleRate, numFrames);
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

            Session = _audioService.OpenDevice(((DeviceSelection)selection.Tag).DeviceId, Graph.GraphId, new ChannelConfig{InputChannels = NumberOfChannels},GetBuffer);
            var s = $"InputDeviceBlock ({DeviceCaps.Name})";
            Name = s;
            Logger.Information("{Name} created", s);
        }

        private AudioBuffer GetBuffer(int numFrames)
        {
            return InternalBuffer;
        }

        protected override void MixInputs(int numFrames)
        {
            
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
        
        protected override void EnableProcessing()
        {
            
            _audioService.OpenStream(Session.SessionId);
        }

        protected override void DisableProcessing()
        {
            _audioService.CloseStream(Session.SessionId);
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