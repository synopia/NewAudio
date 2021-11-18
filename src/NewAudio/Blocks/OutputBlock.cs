using System;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Dsp;

namespace NewAudio.Block
{
    public abstract class OutputBlock : AudioBlock
    {
        private int _counter;
        private long _lag;
        public double LagMs { get; private set; }

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

        public abstract int OutputSampleRate { get; }
        public abstract int OutputFramesPerBlock { get; }

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


        // public abstract int FillBuffer(byte[] buffer, int offset, int count);
    }

    
    public abstract class OutputDeviceBlock : OutputBlock
    {
        public override string Name => $"OutputDeviceBlock ({Device?.Name})";
        protected IDevice Device { get; set; }
        private bool _wasEnabledBeforeParamChange;
        
        protected OutputDeviceBlock(IDevice device, AudioBlockFormat format) : base(format.WithAutoEnable(false))
        {
            Device = device;
            var deviceChannels = Device.NumberOfOutputChannels;
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
        protected void DeviceParamsWillChange()
        {
            _wasEnabledBeforeParamChange = IsEnabled;
            Graph.Disable();
            Graph.UninitializeAllNodes();
        }

        protected void DeviceParamsDidChange()
        {
            Graph.InitializeAllNodes();
            Graph.SetEnabled(_wasEnabledBeforeParamChange);
        }
        
        public override int OutputSampleRate => Device?.SampleRate ?? 0;
        public override int OutputFramesPerBlock => Device?.FramesPerBlock ?? 0;

        public abstract void FillBuffer(IntPtr[] buffers, int numSamples);
    }
}