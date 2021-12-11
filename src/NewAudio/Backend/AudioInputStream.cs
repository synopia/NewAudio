using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Backend
{
    public class AudioInputStream<TSampleType, TMemoryAccess> : AudioStreamBase<TSampleType, TMemoryAccess>, IAudioInputStream
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        private readonly IConvertReader _convertReader;
        public override AudioStreamType Type => AudioStreamType.OnlyInput;

        public AudioInputStream(AudioStreamConfig config) : base(config)
        {
            _convertReader = new ConvertReader<TSampleType, TMemoryAccess>();
            Logger.Information("Audio input stream created: {@This}", this);
        }

        public double InputLatency => Latency.input;

        protected override void Convert(XtBuffer buffer)
        {
            _convertReader.Read(buffer, 0, AudioBuffer!, 0, buffer.frames);
        }

        public override XtChannels CreateChannels()
        {
            return new XtChannels(NumInputChannels, Config.ActiveInputChannels.Mask, 0, 0);
        }

        public AudioBuffer BindInput(XtBuffer buffer)
        {
            return Bind(buffer, NumInputChannels);
        }
    }
}