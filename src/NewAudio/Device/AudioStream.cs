using System;
using System.Buffers;
using System.Diagnostics;
using NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Device
{
    public interface IAudioStreamCallback
    {
        int OnBuffer(XtStream stream, in XtBuffer buffer, object user);

        void OnXRun(XtStream stream, int index, object user);

        void OnRunning(XtStream stream, bool running, ulong error, object user);
    }

    public class AudioStream : IDisposable
    {
        public XtDevice Device { get; }
        public int TotalChannels { get; }
        public bool IsOutput { get; }
        public bool IsInput => !IsOutput;
        public int NumChannels => AudioChannels.Count;
        public AudioChannels AudioChannels { get; }
        public int SampleRate { get; }
        public XtSample SampleType { get; }
        public bool Interleaved { get; }
        public double BufferSize { get; }

        private XtFormat _format;
        private XtStream _stream;

        public int FramesPerBlock { get; private set; }

        private Memory<float>[] _channels;
        private IMemoryOwner<float> _ioBufferSpace;
        private AudioBuffer _audioBuffer;
        public IAudioStreamCallback Callback { get; }
        private XtDeviceStreamParams _deviceParams;
        private IConvertReader _convertReader;
        private IConvertWriter _convertWriter;

        public AudioStream(XtDevice device, int totalChannels, bool interleaved, bool isOutput,
            AudioChannels audioChannels, int sampleRate, XtSample sampleType, double bufferSize,
            IAudioStreamCallback callback)
        {
            Interleaved = interleaved;
            TotalChannels = totalChannels;
            Device = device;
            IsOutput = isOutput;
            AudioChannels = audioChannels;
            SampleRate = sampleRate;
            SampleType = sampleType;
            Callback = callback;
            BufferSize = bufferSize;


        }

        public XtLatency GetLatency()
        {
            return _stream.GetLatency();
        }
        
        public void CreateFullDuplexStream(AudioStream input)
        {
            Trace.Assert(Callback!=null );
            
            XtChannels xtChannels = new XtChannels(input.AudioChannels.Count, /*input.AudioChannels.Mask*/0, AudioChannels.Count, /*AudioChannels.Mask*/0);
            _format = new XtFormat(new XtMix(SampleRate, SampleType), xtChannels);
            XtStreamParams streamParams= new XtStreamParams(Interleaved, Callback.OnBuffer, Callback.OnXRun, Callback.OnRunning);

            _deviceParams = new XtDeviceStreamParams(streamParams, _format, BufferSize);

            Console.WriteLine($"Starting with {SampleType}, {SampleRate}, I={Interleaved}");
            _stream = Device.OpenStream(_deviceParams, null);
            FramesPerBlock = _stream.GetFrames();
            
            SetupAudioBuffer();
            InitSampleConv();
            input.FramesPerBlock = FramesPerBlock;
            input.SetupAudioBuffer();
            input.InitSampleConv();
        }
        public void CreateStream()
        {
            XtChannels xtChannels;
            if (IsOutput)
            {
                xtChannels = new XtChannels(0, 0, AudioChannels.Count, AudioChannels.Mask);
            }
            else
            {
                xtChannels = new XtChannels(AudioChannels.Count, AudioChannels.Mask, 0, 0);
            }

            _format = new XtFormat(new XtMix(SampleRate, SampleType), xtChannels);

            XtStreamParams streamParams;
            if (Callback != null)
            {
                streamParams = new XtStreamParams(Interleaved, Callback.OnBuffer, Callback.OnXRun, Callback.OnRunning);
            }
            else
            {
                streamParams = new XtStreamParams(Interleaved, null, null, null);
            }

            _deviceParams = new XtDeviceStreamParams(streamParams, _format, BufferSize);

            _stream = Device.OpenStream(_deviceParams, this);
            FramesPerBlock = _stream.GetFrames();
            
            SetupAudioBuffer();
            InitSampleConv();
        }


        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _audioBuffer?.Dispose();
                _ioBufferSpace?.Dispose();
                _stream?.Dispose();

                _audioBuffer = null;
                _ioBufferSpace = null;
                _stream = null;
                
            }
        }

        public void Start()
        {
            // if (IsOutput)
            // {
                _stream.Start();
            // }
        }
        
        private void InitSampleConv()
        {
            if (Interleaved)
            {
                if (IsInput)
                {
                    _convertReader = SampleType switch
                    {
                        XtSample.Float32 => new ConvertReader<Float32Sample, Interleaved>(),
                        XtSample.Int16 => new ConvertReader<Int16LsbSample, Interleaved>(),
                        XtSample.Int24 => new ConvertReader<Int24LsbSample, Interleaved>(),
                        XtSample.Int32 => new ConvertReader<Int32LsbSample, Interleaved>(),
                        _ => throw new NotImplementedException()
                    };                    
                }
                else
                {
                    _convertWriter = SampleType switch
                    {
                        XtSample.Float32 => new ConvertWriter<Float32Sample, Interleaved>(),
                        XtSample.Int16 => new ConvertWriter<Int16LsbSample, Interleaved>(),
                        XtSample.Int24 => new ConvertWriter<Int24LsbSample, Interleaved>(),
                        XtSample.Int32 => new ConvertWriter<Int32LsbSample, Interleaved>(),
                        _ => throw new NotImplementedException()
                    };
                }

            }
            else
            {
                if (IsInput)
                {
                    _convertReader = SampleType switch
                    {
                        XtSample.Float32 => null,
                        XtSample.Int16 => new ConvertReader<Int16LsbSample, NonInterleaved>(),
                        XtSample.Int24 => new ConvertReader<Int24LsbSample, NonInterleaved>(),
                        XtSample.Int32 => new ConvertReader<Int32LsbSample, NonInterleaved>(),
                        _ => throw new NotImplementedException()
                    };
                }
                else
                {
                    _convertWriter = SampleType switch
                    {
                        XtSample.Float32 => null,
                        XtSample.Int16 => new ConvertWriter<Int16LsbSample, NonInterleaved>(),
                        XtSample.Int24 => new ConvertWriter<Int24LsbSample, NonInterleaved>(),
                        XtSample.Int32 => new ConvertWriter<Int32LsbSample, NonInterleaved>(),
                        _ => throw new NotImplementedException()
                    };

                }
            }
        }

        public unsafe AudioBuffer Bind(XtBuffer buffer)
        {
            if (SampleType == XtSample.Float32)
            {
                IntPtr ptr = IsInput ? buffer.input : buffer.output;
                for (int i = 0; i < NumChannels; i++)
                {
                    float* data = *((float**)ptr.ToPointer()+i);                    
                    var manager = new UnmanagedMemoryManager<float>(data, FramesPerBlock);
                    _channels[i] = manager.Memory;
                }
            }
            else
            {
                
                if (IsInput)
                {
                    Trace.Assert(_convertReader!=null);
                    _convertReader.Read(buffer, 0, _audioBuffer, 0, FramesPerBlock);
                }
                else
                {
                    Trace.Assert(_convertWriter!=null);
                    _convertWriter.Write(_audioBuffer, 0, buffer, 0, FramesPerBlock);
                }
            }
            return _audioBuffer;
        }

        private void SetupAudioBuffer()
        {
            _ioBufferSpace?.Dispose();

            _channels = new Memory<float>[NumChannels];
            

            if (SampleType != XtSample.Float32)
            {
                int n = 0;
                _ioBufferSpace = MemoryPool<float>.Shared.Rent(NumChannels * FramesPerBlock);

                for (int i = 0; i < TotalChannels; i++)
                {
                    if (AudioChannels[i])
                    {
                        _channels[n] =
                            _ioBufferSpace.Memory.Slice(n * FramesPerBlock, FramesPerBlock);
                        n++;
                    }
                }

                Trace.Assert(n == AudioChannels.Count);
            }
            _audioBuffer = new AudioBuffer(_channels, NumChannels, 0, FramesPerBlock);
        }
    }

}