using System;
using NewAudio.Core;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;
using Xt;

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
        public AudioBuffer RenderInputs()
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
    }

    public class DeviceBlockFormat : AudioBlockFormat
    {
        public int SampleRate;
        public XtSample DefaultSample;

        public float BufferSize;
        public int ChannelOffset;
        
        public DeviceBlockFormat WithChannelOffset(int offset)
        {
            ChannelOffset = offset;
            return this;
        }
        
    }
    public class OutputDeviceBlock : OutputBlock
    {
        public override string Name { get; }
        private IResourceHandle<IXtDevice> _device;
        public IXtDevice Device => _device.Resource;
        private bool _wasEnabledBeforeParamChange;
        public int ChannelOffset { get; set; }
        public readonly DeviceBlockFormat Format;
        
        public int MaxNumberOfChannels => Device.GetChannelCount(true);
        private int _framesPerBlock;
        private int _sampleRate;

        public override int OutputSampleRate => _sampleRate;
        public override int OutputFramesPerBlock => _framesPerBlock;
        

        private IConvertWriter _converter;

        public OutputDeviceBlock(string name, IResourceHandle<IXtDevice> device, DeviceBlockFormat format) : base(format.WithAutoEnable(false))
        {
            var s = $"OutputDeviceBlock ({name})";
            Name = s;
            _device = device;
            Format = format;
            InitLogger<OutputDeviceBlock>();
            Logger.Information("{Name} created ({@Format})", s, Format);
             
            // Device.DeviceParamsWillChange += DeviceParamsWillChange;
            // Device.DeviceParamsDidChange += DeviceParamsDidChange;

            ChannelOffset = format.ChannelOffset;
            
            var deviceChannels = format.Channels;
            if (ChannelMode != ChannelMode.Specified)
            {
                ChannelMode = ChannelMode.Specified;
                NumberOfChannels = Math.Min(deviceChannels, 2);
            }

            if (deviceChannels < NumberOfChannels)
            {
                NumberOfChannels = deviceChannels;
            }

            _sampleRate = Format.SampleRate;
            
            var mix = new XtMix(Format.SampleRate, Format.DefaultSample);
            _streamFormat = new XtFormat(mix, new XtChannels(0, 0, NumberOfChannels, 0));

            var formatOkay = Device.SupportsFormat(_streamFormat);
            if (!formatOkay)
            {
                throw new FormatException();
            }

            var sampleFormat = _streamFormat.mix.sample;
            
            _converter ??= sampleFormat switch
            {
                XtSample.Float32 => new ConvertWriter<Float32Sample, NonInterleaved>(),
                XtSample.Int16 => new ConvertWriter<Int16LsbSample, NonInterleaved>(),
                XtSample.Int24 => new ConvertWriter<Int24LsbSample, NonInterleaved>(),
                XtSample.Int32 => new ConvertWriter<Int32LsbSample, NonInterleaved>(),
                _ => throw new NotImplementedException()
            };
            
            var streamParams = new XtStreamParams(false, OnBuffer, OnRun, OnRunning);
            var deviceParams = new XtDeviceStreamParams(streamParams, _streamFormat, Format.BufferSize);
            _stream = Device.OpenStream(deviceParams, null);

            _framesPerBlock = _stream.GetFrames();

        }

        protected override void Initialize()
        {
            
        }

        protected override void EnableProcessing()
        {
            _stream.Start();
        }

        protected override void DisableProcessing()
        {
            _stream.Stop();
        }

        protected override void Uninitialize()
        {
            _stream.Dispose();
        }

        private int OnBuffer(XtStream stream, in XtBuffer buffer, object user)
        {
            try
            {
                var renderBuffer = RenderInputs();
                if (renderBuffer.NumberOfFrames != FramesPerBlock)
                {
                    return 0;
                }
                _converter.Write(renderBuffer.Data, buffer.output, renderBuffer.NumberOfFrames, renderBuffer.NumberOfChannels);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                Logger.Error(exception, "Error in ASIO Thread");
            }

            return 0;
        }
        
        private void OnRunning(XtStream stream, bool running, ulong error, object user)
        {
            
        }
        private void OnRun(XtStream stream, int index, object user)
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
        private IXtStream _stream;
        private XtFormat _streamFormat;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Device.DeviceParamsWillChange -= DeviceParamsWillChange;
                    // Device.DeviceParamsDidChange -= DeviceParamsDidChange;
                    _stream?.Dispose();
                    _device.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

    }
}