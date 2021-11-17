using NewAudio.Dsp;
using NewAudio.Nodes;

namespace NewAudio.Devices
{
    public class AsioOutput: OutputNode
    {
        public override string NodeName => "AsioOutput";
        public DeviceConfig PlayingParams { get; private set; }
        public override int OutputSampleRate => (int)PlayingParams.SamplingFrequency;
        public override int OutputFramesPerBlock => PlayingParams.FramesPerBlock;
        public AsioOutput() : base(new AudioNodeConfig())
        {
            InitLogger<AsioOutput>();
        }

        public override void UpdateConfig(DeviceConfig config)
        {
            PlayingParams = config;
            NumberOfChannels = PlayingParams.Channels;
            _internalBuffer.SetSize(config.FramesPerBlock, NumberOfChannels);
            _summingBuffer.SetSize(config.FramesPerBlock, NumberOfChannels);
        }

        public override int FillBuffer(byte[] buffer, int offset, int count)
        {
            if (OutputSampleRate == 0 || OutputFramesPerBlock == 0)
            {
                return count;
            }
            _internalBuffer.Zero();
            PullInputs(_internalBuffer);
            if (CheckNotClipping())
            {
                _internalBuffer.Zero();
            }

            var bytesPerFrame = _internalBuffer.BytesPerSample * _internalBuffer.NumberOfChannels;
            Converter.Interleave(_internalBuffer.Data, buffer, count/bytesPerFrame, _internalBuffer.NumberOfChannels, count/bytesPerFrame);

            return count;
        }
    }
}