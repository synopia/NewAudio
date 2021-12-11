using System;
using System.Buffers;
using System.IO;
using NLayer;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Internal
{
    public class Mp3File
    {
        private MpegFile _mpegFile;

        public int Channels => _mpegFile.Channels;
        public ulong Samples => (ulong)_mpegFile.Length / (ulong)Channels / 4;

        public Mp3File(MpegFile mpegFile)
        {
            _mpegFile = mpegFile;
        }

        public static Mp3File Load(string path)
        {
            return new Mp3File(new MpegFile(path));
        }

        public unsafe AudioBuffer ToAudioBuffer()
        {
            var audioBuffer = new AudioBuffer(Channels, (int)Samples);
            IConvertReader reader = new ConvertReader<Float32Sample, Interleaved>();

            var data = ArrayPool<float>.Shared.Rent((int)Samples * Channels);
            _mpegFile.ReadSamples(data, 0, (int)Samples * Channels);
            fixed (float* buf = data)
            {
                var buffer = new XtBuffer()
                {
                    frames = (int)Samples,
                    input = new IntPtr(buf)
                };
                reader.Read(buffer, 0, audioBuffer, 0, (int)Samples);
            }

            ArrayPool<float>.Shared.Return(data);
            return audioBuffer;
        }
    }
}