using System;
using System.IO;
using System.Text;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Internal
{
    public enum WaveFileFormat
    {
        Unknown = 0,
        PCM = 1,
        ADPCM = 2,
        IEEEFloat=3,
        MPEG=5,
        ALaw=6,
        MuLaw=7,
        Extensible=0xFFFE
    }
    public class WaveFile
    {
        public WaveFileFormat Format { get; private set; }
        public WaveFileFormat SubFormat { get; private set; }
        public int Channels { get; private set; }
        public int SampleRate { get; private set; }
        public int BitsPerSample { get; private set; }
        public ArraySegment<byte> Data { get; private set; }

        public int Samples => Data.Count / (BitsPerSample / 8 * Channels);

        public static WaveFile Load(string path)
        {
            return Load(File.ReadAllBytes(path));
        }

        public static WaveFile Load(byte[] data)
        {
            WaveFile wave = new WaveFile();
            var chunkId = Encoding.ASCII.GetString(data, 0, 4);
            int fileSize = (int) (BitConverter.ToUInt32(data, 4) + 8);
            var fileFormatId = Encoding.ASCII.GetString(data, 8, 4);
            if (chunkId != "RIFF" || fileFormatId != "WAVE")
            {
                throw new FormatException();
            }

            for (int i = 12; i < fileSize; )
            {
                chunkId = Encoding.ASCII.GetString(data, i, 4);
                int currentSize = (int)BitConverter.ToUInt32(data, i + 4);
                if (chunkId == "fmt ")
                {
                    wave.Format = (WaveFileFormat)BitConverter.ToUInt16(data, i + 8);
                    wave.Channels = (int)BitConverter.ToUInt16(data, i + 10);
                    wave.SampleRate = (int)BitConverter.ToUInt32(data, i + 12);
                    wave.BitsPerSample = (int)BitConverter.ToUInt16(data, i + 22);
                    if (wave.Format == WaveFileFormat.Extensible && currentSize > 16)
                    {
                        int extChunkSize = (int)BitConverter.ToUInt16(data, i + 24);
                        wave.BitsPerSample = (int)BitConverter.ToUInt16(data, i + 26);
                        wave.SubFormat = (WaveFileFormat)BitConverter.ToUInt16(data, i + 32);
                    }
                } else if (chunkId == "data")
                {
                    wave.Data = new ArraySegment<byte>(data, i + 8, currentSize);
                }

                i += currentSize + 8;
            }

            return wave;
        }

        public unsafe AudioBuffer ToAudioBuffer()
        {
            IConvertReader reader;
            switch (BitsPerSample)
            {
                case 16: reader = new ConvertReader<Int16LsbSample, Interleaved>();
                    break;
                case 24: reader = new ConvertReader<Int24LsbSample, Interleaved>();
                    break;
                case 32:
                    reader = Format == WaveFileFormat.IEEEFloat
                        ? new ConvertReader<Float32Sample, Interleaved>()
                        : new ConvertReader<Int32LsbSample, Interleaved>();
                    break;
                default:
                    throw new FormatException();
            }

            AudioBuffer audioBuffer = new AudioBuffer(Channels, Samples);
            fixed (byte* buf = Data.Array)
            {
                XtBuffer buffer = new XtBuffer()
                {
                    frames = Samples,
                    input = new IntPtr(buf+Data.Offset),
                };
                reader.Read(buffer, 0, audioBuffer, 0, Samples);
            }

            return audioBuffer;
        }
    }
}