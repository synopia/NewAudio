using System;
using NLayer;

namespace VL.NewAudio.Files
{
    public class Mp3FileReader: AudioFileReaderBase
    {
        
        private MpegFile _mpegFile;
        protected override void ReadHeader(string path)
        {
            _mpegFile = new MpegFile(path);
            SampleRate = _mpegFile.SampleRate;
            Channels = _mpegFile.Channels;
            BitsPerSample = 32;
            IsInterleaved = true;
            IsFloatingPoint = true;
            Samples = _mpegFile.Length / Channels / BytesPerSample;
        }

        protected override int ReadData(byte[] data, long startPos, int numBytes)
        {
            // _mpegFile.Position = startPos;
            return _mpegFile.ReadSamples(data, 0, numBytes);
        }
    }
}