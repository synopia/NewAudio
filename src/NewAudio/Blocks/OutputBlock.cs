using System;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;
using Xt;

namespace NewAudio.Block
{
    public abstract class OutputBlock : AudioBlock
    {
        private ulong _lastClip;
        private bool _clipDetectionEnabled;
        private float _clipThreshold;
        public bool IsClipDetectionEnabled => _clipDetectionEnabled;

        public ulong LastClip
        {
            get
            {
                var clip = _lastClip;
                _lastClip = 0;
                return clip;
            }
        }


        protected OutputBlock(AudioBlockFormat format) : base(format)
        {
            _clipDetectionEnabled = true;
            _clipThreshold = 2;
            _lastClip = 0;
            if (!format.IsAutoEnableSet)
            {
                IsAutoEnable = false;
            }
        }

        public void EnableClipDetection(bool enable = true, float threshold = 2)
        {
            // todo lock
            _clipDetectionEnabled = enable;
            _clipThreshold = threshold;
        }

        // public abstract int OutputSampleRate { get; }
        // public abstract int OutputFramesPerBlock { get; }

        public override void Connect(AudioBlock output)
        {
            throw new InvalidOperationException("Not supported!");
        }

        protected bool CheckNotClipping()
        {
            if (_clipDetectionEnabled)
            {
                var recordedFrame = 0;
                if (AudioMath.ThresholdBuffer(InternalBuffer, _clipThreshold, out recordedFrame))
                {
                    _lastClip = Graph.NumberOfProcessedFrames + (ulong)recordedFrame;
                    return true;
                }
            }

            return false;
        }
    
    }

    public class OutputDeviceBlock : OutputBlock
    {
        public override string Name { get; }

        // public override int OutputSampleRate => Session.Format.SampleRate;
        // public override int OutputFramesPerBlock => Session.Format.FramesPerBlock;
        private IAudioService _audioService;
        public Session Session { get; }
        public DeviceCaps DeviceCaps { get; }
        
        public OutputDeviceBlock(OutputDeviceSelection selection, AudioBlockFormat format) : base(format.WithAutoEnable(false))
        {
            _audioService = Resources.GetAudioService();
            InitLogger<OutputDeviceBlock>();

            DeviceCaps = _audioService.GetDeviceInfo((DeviceSelection)selection.Tag);


            var deviceChannels = DeviceCaps.MaxOutputChannels;
            
            if (ChannelMode != ChannelMode.Specified)
            {
                ChannelMode = ChannelMode.Specified;
                NumberOfChannels = Math.Min(deviceChannels, 2);
            }

            if (deviceChannels < NumberOfChannels)
            {
                NumberOfChannels = deviceChannels;
            }
            
            Session = _audioService.OpenDevice(((DeviceSelection)selection.Tag).DeviceId, Graph.GraphId, new ChannelConfig{OutputChannels = NumberOfChannels}, RenderInputs);
            
            var s = $"OutputDeviceBlock ({DeviceCaps.Name})";
            Name = s;
            
            Logger.Information("{Name} created", s);
        }

        protected override bool SupportsProcessInPlace()
        {
            return false;
        }

        public AudioBuffer RenderInputs(int numFrames)
        {
            InternalBuffer.Zero();
            if (IsEnabled)
            {
                PullInputs(InternalBuffer, numFrames);
            }

            if (CheckNotClipping())
            {
                InternalBuffer.Zero();
            }
          
            return InternalBuffer;
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