using NewAudio.Dsp;
using NewAudio.Nodes;
using VL.NewAudio.Internal;

namespace NewAudio.Devices
{
    public class WasapiInput: InputNode
    {
        public override string NodeName => "WasapiInput";
        public DeviceConfig RecordParams { get; private set; }
        private RingBuffer[] _ringBuffers;
        public WasapiInput() : base(new AudioNodeConfig())
        {
            InitLogger<WasapiOutput>();
        }

        public override void OnDataReceived(byte[] buffer)
        {
            
        }

        protected override void Process(AudioBuffer buffer)
        {
            
        }

        public override void UpdateConfig(DeviceConfig config)
        {
            RecordParams = config;
            NumberOfChannels = RecordParams.Channels;
            _ringBuffers = new RingBuffer[NumberOfChannels];
            for (int i = 0; i < NumberOfChannels; i++)
            {
                _ringBuffers[i] = new RingBuffer(2 * config.FramesPerBlock*NumberOfChannels);
            }
        }

    }
}