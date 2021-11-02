using NAudio.Wave;

namespace NewAudio.Core
{
    public enum AudioSampleRate
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

    public struct AudioFormat
    {
        public int SampleRate { get; }
        public int Channels { get; }
        public int SampleCount { get; }
        public int BufferSize => SampleCount * Channels;
        public bool IsInterleaved { get; }

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
    }
}