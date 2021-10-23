using NAudio.Wave;

namespace NewAudio
{
    public readonly struct AudioFormat
    {
        public readonly int Channels;
        public readonly int SampleRate;
        public readonly int BufferSize;
        public readonly int BlockCount;

        public readonly WaveFormat WaveFormat;

        public AudioFormat(int channels, int sampleRate, int bufferSize, int blockCount) : this()
        {
            Channels = channels;
            SampleRate = sampleRate;
            BufferSize = bufferSize;
            BlockCount = blockCount;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public AudioFormat WithChannels(int channels)
        {
            if (channels == Channels)
            {
                return this;
            }
            return new AudioFormat(channels, SampleRate, BufferSize, BlockCount);
        }

        public AudioFormat WithSampleRate(int sampleRate)
        {
            if (sampleRate == SampleRate)
            {
                return this;
            }
            return new AudioFormat(Channels, sampleRate, BufferSize, BlockCount);
            
        }
        public AudioFormat WithBufferSize(int bufferSize)
        {
            if (bufferSize == BufferSize)
            {
                return this;
            }
            return new AudioFormat(Channels, SampleRate, bufferSize, BlockCount);
        }
        public AudioFormat WithBlockCount(int blockCount)
        {
            if (blockCount == BlockCount)
            {
                return this;
            }
            return new AudioFormat(Channels, SampleRate, BufferSize, blockCount);
        }

        public AudioFormat Update(WaveFormat waveFormat)
        {
            return WithSampleRate(waveFormat.SampleRate).WithChannels(waveFormat.Channels);
        }

        public override string ToString()
        {
            return $"{SampleRate}Hz, {Channels}Ch {WaveFormat.BitsPerSample}Bit {BufferSize} samples";
        }
    }
}