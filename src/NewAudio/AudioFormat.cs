using NAudio.Wave;

namespace NewAudio
{
    public readonly struct AudioFormat
    {
        public readonly int Channels;
        public readonly int SampleRate;
        public readonly int SampleCount;
        public readonly int BufferSize => SampleCount*Channels;

        public readonly WaveFormat WaveFormat;

        public AudioFormat(int channels, int sampleRate, int sampleCount) : this()
        {
            Channels = channels;
            SampleRate = sampleRate;
            SampleCount = sampleCount;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public AudioFormat WithChannels(int channels)
        {
            if (channels == Channels)
            {
                return this;
            }
            return new AudioFormat(channels, SampleRate, SampleCount);
        }

        public AudioFormat WithSampleRate(int sampleRate)
        {
            if (sampleRate == SampleRate)
            {
                return this;
            }
            return new AudioFormat(Channels, sampleRate, BufferSize);
            
        }
        public AudioFormat WithBufferSize(int bufferSize)
        {
            if (bufferSize == BufferSize)
            {
                return this;
            }
            return new AudioFormat(Channels, SampleRate, bufferSize/Channels);
        }
        public AudioFormat WithSampleCount(int sampleCount)
        {
            if (sampleCount != SampleCount)
            {
                return this;
            }
            return new AudioFormat(Channels, SampleRate, sampleCount);
        }

        public AudioFormat Update(WaveFormat waveFormat)
        {
            return WithSampleRate(waveFormat.SampleRate).WithChannels(waveFormat.Channels);
        }

        public override string ToString()
        {
            return $"{SampleRate}Hz, {Channels}Ch {WaveFormat?.BitsPerSample}Bit {SampleCount} samples";
        }
    }
}