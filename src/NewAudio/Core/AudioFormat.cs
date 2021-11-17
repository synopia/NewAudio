using NAudio.Wave;

namespace NewAudio.Core
{
    public enum SamplingFrequency
    {
        Hz8000 = 8000,
        Hz11025 = 11025,
        Hz16000 = 16000,
        Hz22050 = 22050,
        Hz32000 = 32000,
        Hz44056 = 44056,
        Hz44100 = 44100,
        Hz48000 = 48000,
        Hz88200 = 88200,
        Hz96000 = 96000,
        Hz176400 = 176400,
        Hz192000 = 192000,
        Hz352800 = 352800
    }

    public readonly struct AudioFormat
    {
        public int SampleRate { get; }
        public int NumberOfChannels { get; }
        public int NumberOfFrames { get; }
        public int BufferSize => NumberOfFrames * NumberOfChannels;
        public bool IsInterleaved { get; }
        public int BytesPerSample => WaveFormat.BitsPerSample / 8;
        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, NumberOfChannels);

        public AudioFormat(int sampleRate, int numberOfFrames, int numberOfChannels = 1, bool isInterleaved = true)
        {
            SampleRate = sampleRate;
            NumberOfChannels = numberOfChannels;
            NumberOfFrames = numberOfFrames;
            IsInterleaved = isInterleaved;
        }

        public override string ToString()
        {
            return $"{SampleRate}Hz, {NumberOfChannels}Ch {WaveFormat?.BitsPerSample}Bit {NumberOfFrames} samples";
        }

        public AudioFormat WithChannels(int channels)
        {
            return new AudioFormat(SampleRate, NumberOfFrames, channels, IsInterleaved);
        }

        public AudioFormat WithSampleCount(int sampleCount)
        {
            return new AudioFormat(SampleRate, sampleCount, NumberOfChannels, IsInterleaved);
        }

        public AudioFormat WithWaveFormat(WaveFormat waveFormat)
        {
            return new AudioFormat(waveFormat.SampleRate, NumberOfFrames, waveFormat.Channels, IsInterleaved);
        }
    }
}