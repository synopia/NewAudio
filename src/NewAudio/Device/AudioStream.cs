using System;
using System.Diagnostics;
using System.Linq;
using NewAudio.Dsp;
using Serilog;
using Xt;

namespace NewAudio.Device
{
    public enum AudioStreamType
    {
        OnlyInput,
        OnlyOutput,
        Aggregate,
        FullDuplex,
        Mixed,
    }
    
    public interface IAudioStreamCallback
    {
        int OnBuffer(XtStream stream, in XtBuffer buffer, object user);

        void OnXRun(XtStream stream, int index, object user);

        void OnRunning(XtStream stream, bool running, ulong error, object user);
    }

    public interface IAudioStream : IDisposable
    {
        AudioStreamType Type { get; }
        int FramesPerBlock { get; }
        int NumInputChannels { get; }
        int NumOutputChannels { get; }
        AudioStreamConfig Config { get; }
        void Start();
        void Open(IAudioStreamCallback? callback);
        AudioBuffer? BindInput(XtBuffer buffer);
        AudioBuffer BindOutput(XtBuffer buffer);
        XtLatency Latency { get; }

        XtChannels CreateChannels();
        AudioBuffer CreateBuffers(int numChannels, int numFrames);
    }

    public abstract class BaseAudioStream<TSampleType, TMemoryAccess> : IAudioStream
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        protected ILogger Logger = Resources.GetLogger<IAudioStream>();
        public abstract bool IsInput { get; }
        public abstract bool IsOutput { get; }
        
        public abstract AudioStreamType Type { get; }

        public AudioStreamConfig Config { get; }

        public XtSample SampleType
        {
            get
            {
                if (typeof(TSampleType) == typeof(Int16LsbSample))
                {
                    return XtSample.Int16;
                }

                if (typeof(TSampleType) == typeof(Int32LsbSample))
                {
                    return XtSample.Int32;
                }

                if (typeof(TSampleType) == typeof(Int24LsbSample))
                {
                    return XtSample.Int24;
                }

                if (typeof(TSampleType) == typeof(Float32Sample))
                {
                    return XtSample.Float32;
                }

                throw new NotImplementedException();
            }
        }

        public XtLatency Latency => Stream?.GetLatency() ?? new XtLatency();

        public XtDevice Device => Config.Device;

        public virtual int NumOutputChannels => Config.ActiveOutputChannels.Count;
        public virtual int NumInputChannels => Config.ActiveInputChannels.Count;

        public virtual int NumChannels
            => IsInput && !IsOutput ? NumInputChannels
                : !IsInput && IsOutput ? NumOutputChannels
                : 0;

        protected XtStream? Stream;
        protected AudioBuffer? AudioBuffer;
        private Memory<float>[]? _channels;
        protected IAudioStreamCallback? Callback;
        private bool _disposedValue;

        public int FramesPerBlock { get; protected set; }

        protected BaseAudioStream(AudioStreamConfig config)
        {
            Config = config;
        }

        public void Start()
        {
            Stream?.Start();
        }
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Logger.Information("Disposing audio stream {@This}", this);
                    Stream?.Stop();
                    Stream?.Dispose();
                    AudioBuffer?.Dispose();
                }

                _disposedValue = true;
            }
        }


        public abstract XtChannels CreateChannels();

        public virtual void Open(IAudioStreamCallback? callback)
        {
            Callback = callback;
            var mix = Config.Mix;
            var format = new XtFormat(mix, CreateChannels());
            if (!Device.SupportsFormat(format))
            {
                throw new InvalidOperationException("Format is not supported by device");
            }

            XtStreamParams streamParams;
            if (Callback != null)
            {
                streamParams = new XtStreamParams(Config.Interleaved, Callback.OnBuffer, Callback.OnXRun,
                    Callback.OnRunning);
            }
            else
            {
                streamParams = new XtStreamParams(Config.Interleaved, null, null, null);
            }

            var deviceParams = new XtDeviceStreamParams(streamParams, format, Config.BufferSize);
            Logger.Information("Opening stream with params={@Params}", deviceParams);
            Stream = Device.OpenStream(deviceParams, this);

            CreateBuffers(NumChannels, Stream.GetFrames());
        }

        public virtual AudioBuffer CreateBuffers(int numChannels, int numFrames)
        {
            FramesPerBlock = numFrames;

            Trace.Assert(numFrames > 0 && numChannels > 0);

            /*
            if (typeof(TSampleType) == typeof(Float32Sample) && typeof(TMemoryAccess) == typeof(NonInterleaved))
            {
                Logger.Information("Creating float buffers (num ch={NumCh}, num frames={NumFrames})", numChannels, numFrames);
                Channels = new Memory<float>[numChannels];
                AudioBuffer = new AudioBuffer(Channels, numChannels, numFrames);
            }
            else
            */
            {
                Logger.Information("Creating convert buffers (num ch={NumCh}, num frames={NumFrames})", numChannels, numFrames);
                AudioBuffer = new AudioBuffer(numChannels, numFrames);
                _channels = AudioBuffer.GetWriteChannels();
            }

            return AudioBuffer;
        }

        public virtual AudioBuffer? BindInput(XtBuffer buffer)
        {
            return null;
        }

        public virtual AudioBuffer BindOutput(XtBuffer buffer)
        {
            return AudioBuffer!;
        }

        protected unsafe AudioBuffer Bind(XtBuffer buffer)
        {
            if (_channels == null || AudioBuffer == null)
            {
                throw new InvalidOperationException("Channels and AudioBuffer cannot be null!");
            }

            Trace.Assert(buffer.frames <= FramesPerBlock);
            Trace.Assert(NumChannels > 0 && NumChannels <= FramesPerBlock);
            AudioBuffer.SetSize(NumChannels, buffer.frames, false, false, true);
            /*
            if (typeof(TSampleType) == typeof(Float32Sample) && typeof(TMemoryAccess) == typeof(NonInterleaved))
            {
                IntPtr ptr = IsInput ? buffer.input : buffer.output;
                for (int i = 0; i < NumChannels; i++)
                {
                    float* data = *((float**)ptr.ToPointer() + i);
                    var manager = new UnmanagedMemoryManager<float>(data, buffer.frames);
                    Channels[i] = manager.Memory;
                }
            }
            else
            */
            {
                Convert(buffer);
            }
            
            return AudioBuffer;
        }

        protected abstract void Convert(XtBuffer buffer);
    }

    public class AudioInputStream<TSampleType, TMemoryAccess> : BaseAudioStream<TSampleType, TMemoryAccess>
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        private readonly IConvertReader _convertReader;
        public override bool IsInput => true;
        public override bool IsOutput => false;
        public override AudioStreamType Type => AudioStreamType.OnlyInput;

        public AudioInputStream(AudioStreamConfig config) : base(config)
        {
            _convertReader = new ConvertReader<TSampleType, TMemoryAccess>();
            Logger.Information("Audio input stream created: {@This}", this);
        }

        protected override void Convert(XtBuffer buffer)
        {
            _convertReader.Read(buffer, 0, AudioBuffer!, 0, buffer.frames);
        }

        public override XtChannels CreateChannels()
        {
            return new XtChannels(NumInputChannels, Config.ActiveInputChannels.Mask, 0, 0);
        }

        public override AudioBuffer BindInput(XtBuffer buffer)
        {
            return Bind(buffer);
        }
    }

    public class AudioOutputStream<TSampleType, TMemoryAccess> : BaseAudioStream<TSampleType, TMemoryAccess>
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        private readonly IConvertWriter _convertWriter;
        public override bool IsInput => false;
        public override bool IsOutput => true;

        public AudioOutputStream(AudioStreamConfig config) : base(config)
        {
            _convertWriter = new ConvertWriter<TSampleType, TMemoryAccess>();
            Logger.Information("Audio output stream created: {@This}", this);
        }
        public override AudioStreamType Type => AudioStreamType.OnlyOutput;

        protected override void Convert(XtBuffer buffer)
        {
            _convertWriter.Write(AudioBuffer!, 0, buffer, 0, buffer.frames);
        }

        public override XtChannels CreateChannels()
        {
            return new XtChannels(0, 0, NumOutputChannels, Config.ActiveOutputChannels.Mask);
        }

        public override AudioBuffer BindOutput(XtBuffer buffer)
        {
            return Bind(buffer);
        }
    }

    public class FullDuplexAudioStream<TSampleType, TMemoryAccess> : AudioOutputStream<TSampleType, TMemoryAccess>
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        public override bool IsInput => true;
        public override bool IsOutput => true;
        private readonly AudioInputStream<TSampleType, TMemoryAccess> _inputStream;
        private bool _disposed;

        public override int NumOutputChannels { get; }
        public override int NumInputChannels { get; }
        public override int NumChannels => NumOutputChannels;
        public override AudioStreamType Type => AudioStreamType.FullDuplex;

        public FullDuplexAudioStream(AudioStreamConfig config)
            : base(config)
        {
            _inputStream = new AudioInputStream<TSampleType, TMemoryAccess>(config);
            NumInputChannels = config.ActiveInputChannels.Count;
            NumOutputChannels = config.ActiveOutputChannels.Count;
            Logger.Information("Audio full duplex stream created: {@This}", this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _disposed = true;
                    _inputStream.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public override void Open(IAudioStreamCallback? callback)
        {
            base.Open(callback);

            _inputStream.CreateBuffers(NumInputChannels, FramesPerBlock);
        }

        public override AudioBuffer? BindInput(XtBuffer buffer)
        {
            return _inputStream.BindInput(buffer);
        }

        public override XtChannels CreateChannels()
        {
            return new XtChannels(NumInputChannels, Config.ActiveInputChannels.Mask,
                NumOutputChannels, Config.ActiveOutputChannels.Mask);
        }
    }

    public class AggregateAudioStream<TSampleType, TMemoryAccess> : AudioOutputStream<TSampleType, TMemoryAccess>
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        public override bool IsInput => true;
        public override bool IsOutput => true;
        public override int NumOutputChannels { get; }
        public override int NumInputChannels { get; }
        public override int NumChannels => NumOutputChannels;
        public override AudioStreamType Type => AudioStreamType.Aggregate;

        private readonly IAudioStream _inputStream;
        private readonly AudioStreamConfig[] _configs;
        private bool _disposed;
        
        public AggregateAudioStream(AudioStreamConfig primary, AudioStreamConfig[] secondary)
            : base(primary)
        {
            _configs = secondary.Where(i => i.ActiveInputChannels.Count > 0).ToArray();
            
            var numInputChannels = _configs.Sum(i => i.ActiveInputChannels.Count) + primary.ActiveInputChannels.Count;
            NumOutputChannels = primary.ActiveOutputChannels.Count;
            NumInputChannels = numInputChannels;
            _inputStream = new AudioInputStream<TSampleType, TMemoryAccess>(new AudioStreamConfig(null)
            {
                Interleaved = primary.Interleaved,
                BufferSize = primary.BufferSize,
                SampleRate = primary.SampleRate,
                SampleType = primary.SampleType,
                ActiveInputChannels = AudioChannels.Channels(numInputChannels),
                ActiveOutputChannels = AudioChannels.Disabled
            });

            Logger.Information("Audio aggregate stream created: {@This}", this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _disposed = true;
                    _inputStream.Dispose();
                }
            }
            base.Dispose(disposing);
        }
        
        public override void Open(IAudioStreamCallback? callback)
        {
            Callback = callback;
            var mix = new XtMix(Config.SampleRate, SampleType);

            var outputChannels = CreateChannels();

            XtStreamParams streamParams;
            if (Callback != null)
            {
                streamParams = new XtStreamParams(Config.Interleaved, Callback.OnBuffer, Callback.OnXRun,
                    Callback.OnRunning);
            }
            else
            {
                streamParams = new XtStreamParams(Config.Interleaved, null, null, null);
            }

            double bufSize = Config.BufferSize;
            var aggregateDeviceParams =
                _configs.Select(i
                        => new XtAggregateDeviceParams(i.Device, new XtChannels(i.ActiveInputChannels.Count, i.ActiveInputChannels.Mask,0,0), bufSize))
                    .Concat(new[] { new XtAggregateDeviceParams(Device, outputChannels, bufSize) }).ToArray();

            var deviceParams = new XtAggregateStreamParams(streamParams, aggregateDeviceParams,
                aggregateDeviceParams.Length, mix, Device);

            Stream = Config.Service.AggregateStream(deviceParams, this);

            FramesPerBlock = Stream.GetFrames();

            _inputStream.CreateBuffers(NumInputChannels, FramesPerBlock);

            CreateBuffers(NumOutputChannels, FramesPerBlock);
        }

        public override AudioBuffer? BindInput(XtBuffer buffer)
        {
            return _inputStream.BindInput(buffer);
        }
    }
}