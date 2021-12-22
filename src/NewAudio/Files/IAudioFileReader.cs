using System;
using System.Buffers;
using Serilog;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Files
{
    public interface IAudioFileReader : IDisposable
    {
        int SampleRate { get; }
        long Samples { get; }
        int Channels { get; }
        int BitsPerSample { get; }
        bool IsFloatingPoint { get; }
        bool IsInterleaved { get; }

        void Open(string path);
        void Read(AudioBufferToFill info, long startPos);
    }

    public abstract class AudioFileReaderBase : IAudioFileReader
    {
        private readonly byte[] _buffer;
        public int SampleRate { get; protected set; }
        public long Samples { get; protected set; }
        public int Channels { get; protected set; }
        public int BitsPerSample { get; protected set; }
        public int BytesPerSample => BitsPerSample / 8;
        public bool IsFloatingPoint { get; protected set; }
        public bool IsInterleaved { get; protected set; }
        protected abstract int ReadData(byte[] data, long startPos, int numBytes);
        private IConvertReader? _convertReader;

        protected AudioFileReaderBase()
        {
            _buffer = ArrayPool<byte>.Shared.Rent(1024);
        }

        protected abstract void ReadHeader(string path);

        public void Open(string path)
        {
            ReadHeader(path);
            _convertReader = CreateReader();
        }


        public virtual unsafe void Read(AudioBufferToFill info, long startPos)
        {
            int numSamples = Math.Min(info.NumFrames, (int)(Samples - startPos));
            if (numSamples <= 0)
            {
                info.Buffer.Zero();
                return;
            }

            int numBytes = numSamples * BytesPerSample * Channels;

            long pos = startPos * BytesPerSample * Channels;
            int audioBufferPos = info.StartFrame;
            while (numBytes > 0)
            {
                var read = ReadData(_buffer, pos, Math.Min(numBytes, _buffer.Length));
                if (read == 0) 
                {
                    // eof
                    break;
                }
                var frames = read / BytesPerSample / Channels;
                fixed (byte* b = _buffer)
                {
                    var inputBuffer = new XtBuffer()
                    {
                        frames = frames,
                        input = new IntPtr(b)
                    };

                    _convertReader!.Read(inputBuffer, 0, info.Buffer, audioBufferPos, frames);
                }

                audioBufferPos += frames;
                pos += read;
                numBytes -= read;
            }

            if (audioBufferPos < info.NumFrames)
            {
                info.Buffer.Zero(audioBufferPos, info.NumFrames - audioBufferPos);
            }
        }


        private IConvertReader CreateReader()
        {
            IConvertReader reader;
            if (IsInterleaved)
            {
                switch (BitsPerSample)
                {
                    case 16:
                        reader = new ConvertReader<Int16LsbSample, Interleaved>();
                        break;
                    case 24:
                        reader = new ConvertReader<Int24LsbSample, Interleaved>();
                        break;
                    case 32:
                        reader = IsFloatingPoint
                            ? new ConvertReader<Float32Sample, Interleaved>()
                            : new ConvertReader<Int32LsbSample, Interleaved>();
                        break;
                    default:
                        throw new FormatException();
                }
            }
            else
            {
                switch (BitsPerSample)
                {
                    case 16:
                        reader = new ConvertReader<Int16LsbSample, NonInterleaved>();
                        break;
                    case 24:
                        reader = new ConvertReader<Int24LsbSample, NonInterleaved>();
                        break;
                    case 32:
                        reader = IsFloatingPoint
                            ? new ConvertReader<Float32Sample, NonInterleaved>()
                            : new ConvertReader<Int32LsbSample, NonInterleaved>();
                        break;
                    default:
                        throw new FormatException();
                }
            }

            return reader;
        }


        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                }

                _disposedValue = true;
            }
        }
    }
}