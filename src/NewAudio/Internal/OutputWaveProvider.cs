using NAudio.Wave;
using NewAudio.Core;
using SharedMemory;

namespace NewAudio.Internal
{
    public class OutputWaveProvider : IWaveProvider
    {
        public WaveFormat WaveFormat => AudioFormat.WaveFormat;
        private CircularBuffer _circularBuffer;
        public AudioFormat AudioFormat { get; }
        
        public OutputWaveProvider(AudioFormat format, int nodeCount=16)
        {
            AudioFormat = format;
            _circularBuffer = new CircularBuffer("VL.NewAudio.Output", nodeCount, format.BufferSize*4);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _circularBuffer.Read(buffer, offset);
        }

    }
}