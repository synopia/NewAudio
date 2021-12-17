using System;
using System.Threading;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Backend
{
    public class AudioOutputStream<TSampleType, TMemoryAccess> : AudioStreamBase<TSampleType, TMemoryAccess>,
        IAudioOutputStream
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        private readonly IConvertWriter _convertWriter;
        public override AudioStreamType Type => AudioStreamType.OnlyOutput;

        public AudioOutputStream(AudioStreamConfig config) : base(config)
        {
            _convertWriter = new ConvertWriter<TSampleType, TMemoryAccess>();
            Logger.Information("Audio output stream created: {@This}", this);
        }

        public void Start()
        {
            Stream?.Start();
        }

        public void Stop()
        {
            Stream?.Stop();
        }

        public double OutputLatency => Latency.output;

        protected override void Convert(XtBuffer buffer)
        {
            _convertWriter.Write(AudioBuffer!, 0, buffer, 0, buffer.frames);
        }

        public override XtChannels CreateChannels()
        {
            return new XtChannels(0, 0, NumOutputChannels, Config.ActiveOutputChannels.Mask);
        }

        public AudioBuffer BindOutput(XtBuffer buffer)
        {
            return Bind(buffer, NumOutputChannels);
        }

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
            Stream = XtDevice.OpenStream(deviceParams, this);

            CreateBuffers(NumOutputChannels, Stream.GetFrames());
        }
    }

    public class
        AudioOutputWithNoInputStream<TSampleType, TMemoryAccess> : AudioOutputStream<TSampleType, TMemoryAccess>,
            IAudioInputOutputStream
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        private bool _disposed;

        public AudioOutputWithNoInputStream(AudioStreamConfig config) : base(config)
        {
        }

        public AudioBuffer? BindInput(XtBuffer buffer)
        {
            return null;
        }

        public double InputLatency => 0;
    }
}