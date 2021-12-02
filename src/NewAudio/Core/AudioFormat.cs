using NAudio.Wave;

namespace NewAudio.Core
{
    

    public readonly struct AudioFormat
    {
        public int SampleRate { get; }
        public int Channels { get; }
        public int SampleCount { get; }
        public int BufferSize => SampleCount * Channels;
        public bool IsInterleaved { get; }

        public int BytesPerSample => WaveFormat.BitsPerSample / 8;
        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

        public AudioFormat(int sampleRate, int sampleCount, int channels = 1, bool isInterleaved = true)
        {
            SampleRate = sampleRate;
            Channels = channels;
            SampleCount = sampleCount;
            IsInterleaved = isInterleaved;
        }

        public override string ToString()
        {
            return $"{SampleRate}Hz, {Channels}Ch {WaveFormat?.BitsPerSample}Bit {SampleCount} samples";
        }

        public AudioFormat WithChannels(int channels)
        {
            return new AudioFormat(SampleRate, SampleCount, channels, IsInterleaved);
        }

        public AudioFormat WithSampleCount(int sampleCount)
        {
            return new AudioFormat(SampleRate, sampleCount, Channels, IsInterleaved);
        }

        public AudioFormat WithWaveFormat(WaveFormat waveFormat)
        {
            return new AudioFormat(waveFormat.SampleRate, SampleCount, waveFormat.Channels, IsInterleaved);
        }
    }
}