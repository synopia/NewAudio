using NAudio.Wave;
using NewAudio.Core;
using SharedMemory;

namespace NewAudio.Internal
{
    public class OutputWaveProvider : IWaveProvider
    {
        private readonly CircularBuffer _circularBuffer;

        public OutputWaveProvider(AudioFormat format, int nodeCount = 16)
        {
            AudioFormat = format;
            _circularBuffer = new CircularBuffer("VL.NewAudio.Output", nodeCount, format.BufferSize * 4);
        }

        public AudioFormat AudioFormat { get; }
        public WaveFormat WaveFormat => AudioFormat.WaveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            return _circularBuffer.Read(buffer, offset);
        }
    }
}