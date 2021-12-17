using System;
using System.Diagnostics;
using VL.NewAudio.Dsp;
using Serilog;
using VL.NewAudio.Core;
using Xt;

namespace VL.NewAudio.Backend
{
    public abstract class AudioStreamBase<TSampleType, TMemoryAccess> : IAudioStream
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        protected readonly ILogger Logger = Resources.GetLogger<IAudioStream>();
        public abstract AudioStreamType Type { get; }
        public AudioStreamConfig Config { get; }

        protected XtSample SampleType
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

        protected XtLatency Latency => Stream?.GetLatency() ?? new XtLatency();

        protected IAudioDevice Device => Config.AudioDevice;
        protected XtDevice XtDevice => ((XtAudioDevice)Device).XtDevice;
        protected XtService XtService => ((XtAudioDevice)Device).XtService;

        public virtual int NumOutputChannels => Config.ActiveOutputChannels.Count;
        public virtual int NumInputChannels => Config.ActiveInputChannels.Count;

        protected XtStream? Stream;
        protected AudioBuffer? AudioBuffer;
        private AudioChannel[]? _channels;
        protected IAudioStreamCallback? Callback;
        private bool _disposedValue;

        public int FramesPerBlock { get; protected set; }

        protected AudioStreamBase(AudioStreamConfig config)
        {
            Config = config;
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

        public void CreateBuffers(int numChannels, int numFrames)
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
                Logger.Information("Creating convert buffers (num ch={NumCh}, num frames={NumFrames})", numChannels,
                    numFrames);
                AudioBuffer = new AudioBuffer(numChannels, numFrames);
                _channels = AudioBuffer.GetWriteChannels();
            }
        }

        protected AudioBuffer Bind(XtBuffer buffer, int numChannels)
        {
            if (_channels == null || AudioBuffer == null)
            {
                throw new InvalidOperationException("Channels and AudioBuffer cannot be null!");
            }

            Trace.Assert(buffer.frames <= FramesPerBlock);
            Trace.Assert(numChannels > 0 && numChannels <= FramesPerBlock);
            AudioBuffer.SetSize(numChannels, buffer.frames, false, false, true);
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
}