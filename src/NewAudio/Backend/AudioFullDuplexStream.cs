using VL.NewAudio.Device;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Backend
{
    public class AudioFullDuplexStream<TSampleType, TMemoryAccess> : AudioOutputStream<TSampleType, TMemoryAccess>, IAudioInputOutputStream
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        private readonly AudioInputStream<TSampleType, TMemoryAccess> _inputStream;
        private bool _disposed;
        public override int NumOutputChannels { get; }
        public override int NumInputChannels { get; }
        public override AudioStreamType Type => AudioStreamType.FullDuplex;

        public AudioFullDuplexStream(AudioStreamConfig config)
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
                    Stop();
                    _inputStream.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public double InputLatency => Latency.input;

        public override void Open(IAudioStreamCallback? callback)
        {
            base.Open(callback);

            _inputStream.CreateBuffers(NumInputChannels, FramesPerBlock);
        }

        public AudioBuffer BindInput(XtBuffer buffer)
        {
            return _inputStream.BindInput(buffer);
        }

        public override XtChannels CreateChannels()
        {
            return new XtChannels(NumInputChannels, Config.ActiveInputChannels.Mask,
                NumOutputChannels, Config.ActiveOutputChannels.Mask);
        }
    }
}