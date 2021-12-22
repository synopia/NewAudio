using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace VL.NewAudio.Files
{
    internal static class FileStreamHelper
    {
        public static string ReadString(this FileStream stream, int len)
        {
            byte[] buf = ArrayPool<byte>.Shared.Rent(len);
            stream.Read(buf, 0, len);
            var result = Encoding.ASCII.GetString(buf, 0, len);
            ArrayPool<byte>.Shared.Return(buf);
            return result;
        }

        public static int ReadUInt32(this FileStream stream)
        {
            byte[] buf = ArrayPool<byte>.Shared.Rent(4);
            stream.Read(buf, 0, 4);
            var result = (int)BitConverter.ToUInt32(buf, 0);
            ArrayPool<byte>.Shared.Return(buf);
            return result;
        }
        public static int ReadUInt16(this FileStream stream)
        {
            byte[] buf = ArrayPool<byte>.Shared.Rent(2);
            stream.Read(buf, 0, 2);
            var result = (int)BitConverter.ToUInt16(buf, 0);
            ArrayPool<byte>.Shared.Return(buf);
            return result;
        }
        public static void SkipBytes(this FileStream stream, int bytes)
        {
            byte[] buf = ArrayPool<byte>.Shared.Rent(bytes);
            stream.Read(buf, 0, bytes);
            ArrayPool<byte>.Shared.Return(buf);
        }

        
    }
    public enum WaveFileFormat
    {
        Unknown = 0,
        PCM = 1,
        ADPCM = 2,
        IEEEFloat = 3,
        MPEG = 5,
        ALaw = 6,
        MuLaw = 7,
        Extensible = 0xFFFE
    }
    public class WavFileReader: AudioFileReaderBase
    {
        private FileStream _fileStream;
        private List<(long,long, int)> _chunks = new();
        protected override void ReadHeader(string path)
        {
            _fileStream = File.OpenRead(path);
            Trace.Assert(_fileStream.CanRead && _fileStream.CanSeek);

            var chunkId = _fileStream.ReadString(4);
            var fileSize = _fileStream.ReadUInt32();
            var fileFormatId = _fileStream.ReadString(4);
            if (chunkId != "RIFF" || fileFormatId != "WAVE")
            {
                throw new FormatException();
            }

            long pos = 12;
            long bytePos = 0;
            while (pos<fileSize)
            {
                _fileStream.Seek(pos, SeekOrigin.Begin);
                chunkId = _fileStream.ReadString(4);
                var currentChunkSize = _fileStream.ReadUInt32();
                if (chunkId == "fmt ")
                {
                    var format = (WaveFileFormat)_fileStream.ReadUInt16();
                    IsFloatingPoint = format == WaveFileFormat.IEEEFloat;
                    Channels = _fileStream.ReadUInt16();
                    SampleRate = _fileStream.ReadUInt32();
                    _fileStream.SkipBytes(6);
                    BitsPerSample = _fileStream.ReadUInt16();
                    if (format == WaveFileFormat.Extensible && currentChunkSize > 16)
                    {
                        var extChunkSize = _fileStream.ReadUInt16();
                        BitsPerSample = _fileStream.ReadUInt16();
                        _fileStream.SkipBytes(4);
                        var subFormat = (WaveFileFormat)_fileStream.ReadUInt16();
                    }
                } else if (chunkId == "data")
                {
                    _chunks.Add((bytePos, _fileStream.Position+8, currentChunkSize));
                    bytePos += currentChunkSize;
                }

                pos += currentChunkSize + 8;
            }

            IsInterleaved = true;
            
            Samples = bytePos / BytesPerSample / Channels;
        }

        private (long,long,int) FindChunk(long pos)
        {
            for (int i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i].Item1 <= pos && pos<_chunks[i].Item1+_chunks[i].Item3)
                {
                    return _chunks[i];
                }
            }

            throw new FormatException();
        }
        
        protected override int ReadData(byte[] data, long startPos, int numBytes)
        {
            var (chunkStartPos, filePos, len) = FindChunk(startPos);
            _fileStream.Seek(filePos + startPos, SeekOrigin.Begin);
            var toRead = Math.Min(numBytes, len);
            return _fileStream.Read(data, 0, toRead);
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _fileStream.Close();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}