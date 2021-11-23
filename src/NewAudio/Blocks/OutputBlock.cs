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
    
    }

    public class OutputDeviceBlock : OutputBlock
    {
        public override string Name { get; }
        private IResourceHandle<Device> _device;
        public Device Device { get=>_device.Resource; }

        private bool _wasEnabledBeforeParamChange;

        public override int OutputSampleRate => Device.SampleRate;
        public override int OutputFramesPerBlock => Device.FramesPerBlock;

        public OutputDeviceBlock(string name, IResourceHandle<Device> device, AudioBlockFormat format) : base(format.WithAutoEnable(false))
        {
            var s = $"OutputDeviceBlock ({name})";
            Name = s;
            _device = device;
            InitLogger<OutputDeviceBlock>();
            
            Logger.Information("{Name} created", s);

            Device.DeviceFormatWillChange += DeviceParamsWillChange;
            Device.DeviceFormatDidChange += DeviceParamsDidChange;

            var deviceChannels = Device.MaxNumberOfOutputChannels;
            
            if (ChannelMode != ChannelMode.Specified)
            {
                ChannelMode = ChannelMode.Specified;
                NumberOfChannels = Math.Min(deviceChannels, 2);
            }

            if (deviceChannels < NumberOfChannels)
            {
                NumberOfChannels = deviceChannels;
            }
            
            Device.AttachOutput(this);
        }

        protected override bool SupportsProcessInPlace()
        {
            return false;
        }

        public AudioBuffer RenderInputs()
        {
            Graph.PreProcess();
            
            InternalBuffer.Zero();
            if (IsEnabled)
            {
                PullInputs(InternalBuffer);
            }

            if (CheckNotClipping())
            {
                InternalBuffer.Zero();
            }
          
            Graph.PostProcess();

            return InternalBuffer;
        }
        
        protected override void Initialize()
        {
        }

        protected override void Uninitialize()
        {
        }

        private void DeviceParamsWillChange()
        {
            _wasEnabledBeforeParamChange = IsEnabled;
            Graph.Disable();
            Graph.UninitializeAllNodes();
        }

        private void DeviceParamsDidChange()
        {
            Graph.InitializeAllNodes();
            Graph.SetEnabled(_wasEnabledBeforeParamChange);
        }
        
        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    SetEnabled(false);
                    Device.DeviceFormatWillChange -= DeviceParamsWillChange;
                    Device.DeviceFormatDidChange -= DeviceParamsDidChange;
                    Device.DetachOutput(this);
                    _device.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

    }
}