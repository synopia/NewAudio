using System;
using System.Buffers;
using System.Diagnostics;
using NewAudio.Core;
using NewAudio.Dsp;
using Xt;


namespace NewAudio.Device
{
    
    public interface IAudioStreamCallback 
    {
        int OnBuffer(XtStream stream, in XtBuffer buffer, object user);

        void OnXRun(XtStream stream, int index, object user);

        void OnRunning(XtStream stream, bool running, ulong error, object user);
    }

    public interface IAudioStream : IDisposable
    {
        int FramesPerBlock { get; }
        void Start();
        AudioBuffer Bind(XtBuffer buffer);
        XtLatency Latency { get; }
    }

    public abstract class BaseAudioStream<TSampleType, TMemoryAccess> : IAudioStream where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        public abstract bool IsInput { get; }
        public abstract bool IsOutput { get; }

        public int NumberChannels { get; }
        public AudioChannels ActiveChannels { get; }
        public bool Interleaved => typeof(TMemoryAccess) == typeof(Interleaved);

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
        public XtLatency Latency => _stream.GetLatency();

        public XtDevice Device { get; }
        private readonly XtStream _stream;
        protected readonly AudioBuffer AudioBuffer;
        private readonly Memory<float>[] _channels;
        public int FramesPerBlock { get; }

        protected BaseAudioStream(XtDevice device, AudioChannels activeChannels, int sampleRate, double bufferSize,
            IAudioStreamCallback? callback, XtChannels xtChannels)
        {
            Device = device;
            ActiveChannels = activeChannels;
            NumberChannels = ActiveChannels.Count;
            
            var format = new XtFormat(new XtMix(sampleRate, SampleType), xtChannels);

            XtStreamParams streamParams;
            if (callback != null)
            {
                streamParams = new XtStreamParams(Interleaved, callback.OnBuffer, callback.OnXRun, callback.OnRunning);
            }
            else
            {
                streamParams = new XtStreamParams(Interleaved, null, null, null);
            }

            var deviceParams = new XtDeviceStreamParams(streamParams, format, bufferSize);

            _stream = Device.OpenStream(deviceParams, this);
            FramesPerBlock = _stream.GetFrames();

            if (typeof(TSampleType) == typeof(Float32Sample) && typeof(TMemoryAccess) == typeof(NonInterleaved))
            {
                _channels = new Memory<float>[NumberChannels];
                AudioBuffer = new AudioBuffer(_channels, NumberChannels, FramesPerBlock);
            }
            else
            {
                AudioBuffer = new AudioBuffer(NumberChannels, FramesPerBlock);
                _channels = AudioBuffer.GetWriteChannels();
            }
        }


        public void Start()
        {
            // if (IsOutput)
            // {
            _stream.Start();
            // }
        }

        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                AudioBuffer.Dispose();
                _stream.Dispose();
            }
        }

        public unsafe AudioBuffer Bind(XtBuffer buffer)
        {
            if (typeof(TSampleType) == typeof(Float32Sample) && typeof(TMemoryAccess) == typeof(NonInterleaved))
            {
                IntPtr ptr = IsInput ? buffer.input : buffer.output;
                for (int i = 0; i < NumberChannels; i++)
                {
                    float* data = *((float**)ptr.ToPointer()+i);                    
                    var manager = new UnmanagedMemoryManager<float>(data, FramesPerBlock);
                    _channels[i] = manager.Memory;
                }
            }
            else
            {
                Convert(buffer);
            }

            return AudioBuffer;
        }

        protected abstract void Convert(XtBuffer buffer);

    }
    
    public class AudioInputStream<TSampleType, TMemoryAccess> : BaseAudioStream<TSampleType, TMemoryAccess> where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        private readonly IConvertReader _convertReader;
        public override bool IsInput => true;
        public override bool IsOutput => false;
        public AudioInputStream(XtDevice device, AudioChannels activeChannels, int sampleRate, double bufferSize, IAudioStreamCallback? callback) : base(device,  activeChannels, sampleRate, bufferSize, callback, 
            new XtChannels(activeChannels.Count, activeChannels.Mask, 0,0))
        {
            _convertReader = new ConvertReader<TSampleType, TMemoryAccess>();
        }

        protected override void Convert(XtBuffer buffer)
        {
            _convertReader.Read(buffer, 0, AudioBuffer, 0, FramesPerBlock);
        }

    }
    public class AudioOutputStream<TSampleType, TMemoryAccess> : BaseAudioStream<TSampleType, TMemoryAccess> where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        private readonly IConvertWriter _convertWriter;
        public override bool IsInput => false;
        public override bool IsOutput => true;

        protected AudioOutputStream(XtDevice device, AudioChannels activeChannels, int sampleRate, double bufferSize, IAudioStreamCallback? callback, XtChannels xtChannels) : base(device, activeChannels, sampleRate, bufferSize, callback, 
            xtChannels)
        {
            _convertWriter = new ConvertWriter<TSampleType, TMemoryAccess>();
        }
        public AudioOutputStream(XtDevice device, AudioChannels activeChannels, int sampleRate, double bufferSize, IAudioStreamCallback? callback) : this(device, activeChannels, sampleRate, bufferSize, callback, 
            new XtChannels(0,0, activeChannels.Count, activeChannels.Mask ))
        {
        }

        protected override void Convert(XtBuffer buffer)
        {
            _convertWriter.Write(AudioBuffer, 0, buffer, 0, FramesPerBlock);
        }
    }
    
    public class AudioFullDuplexStream<TSampleType, TMemoryAccess> :AudioOutputStream<TSampleType, TMemoryAccess> where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        public AudioFullDuplexStream(AudioInputStream<TSampleType, TMemoryAccess> inputStream, AudioChannels activeChannels, int sampleRate, double bufferSize, IAudioStreamCallback? callback) : base(inputStream.Device, activeChannels, sampleRate, bufferSize, callback,
            new XtChannels(inputStream.ActiveChannels.Count, inputStream.ActiveChannels.Mask, activeChannels.Count, activeChannels.Mask))
        {
            
        }
    }

}