using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Files
{
    internal record Block
    {
        public Block(IAudioFileReader reader, long startPos, int length)
        {
            StartPos = startPos;
            EndPos = StartPos + length;
            AudioBuffer = new AudioBuffer(reader.Channels, length);
            reader.Read(new AudioSourceChannelInfo(AudioBuffer, 0, length), startPos);
        }

        public long StartPos { get; }
        public long EndPos { get; }
        public AudioBuffer AudioBuffer { get; }

        public bool Intersects(long pos, long endPos)
        {
            return pos < EndPos && StartPos < endPos;
        }
    }

    public class AudioFileBufferedReader : IAudioFileReader, IDisposable
    {
        private List<Block> _blocks = new();
        private readonly IAudioFileReader _source;
        private readonly int _numBlocks;
        private const int SamplesPerBlock = 1 << 15;

        private long _nextReadPosition = 0;
        public int SampleRate => _source.SampleRate;
        public long Samples => _source.Samples;
        public int Channels => _source.Channels;
        public int BitsPerSample => _source.BitsPerSample;
        public bool IsFloatingPoint => _source.IsFloatingPoint;
        public bool IsInterleaved => _source.IsInterleaved;
        private CancellationTokenSource _cts = new();
        private Task? _fillBufferTask;
        private readonly object _lock = new();
        private bool _disposedValue;

        public AudioFileBufferedReader(IAudioFileReader source, int samplesToBuffer)
        {
            _source = source;
            _numBlocks = 1 + samplesToBuffer / SamplesPerBlock;
        }

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
                StopFillBlockTask();
            }
        }

        public void Open(string path)
        {
            _source.Open(path);
            StartFillBlockTask();
        }

        private void StartFillBlockTask()
        {
            _cts = new CancellationTokenSource();
            _fillBufferTask = Task.Run(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var r = ReadNextBufferChunk();
                    Thread.Sleep(TimeSpan.FromMilliseconds(r ? 1 : 100));
                }
            }, _cts.Token);
        }

        private void StopFillBlockTask()
        {
            if (_fillBufferTask != null)
            {
                _cts.Cancel();
                _fillBufferTask.GetAwaiter().GetResult();
            }

            _fillBufferTask = null;
        }

        private Block? FindBlock(long pos)
        {
            foreach (var block in _blocks)
            {
                if (block.StartPos <= pos && pos < block.EndPos)
                {
                    return block;
                }
            }

            return null;
        }

        private bool ReadNextBufferChunk()
        {
            long pos = (_nextReadPosition / SamplesPerBlock) * SamplesPerBlock;
            long endPos = Math.Min(Samples, pos + _numBlocks * SamplesPerBlock);

            List<Block> newBlocks = new List<Block>();
            for (int i = _blocks.Count - 1; i >= 0; i--)
            {
                if (_blocks[i].Intersects(pos, endPos))
                {
                    newBlocks.Add(_blocks[i]);
                }
            }

            if (newBlocks.Count == _numBlocks)
            {
                return false;
            }

            for (long p = pos; p < endPos; p += SamplesPerBlock)
            {
                if (FindBlock(p) == null)
                {
                    newBlocks.Add(new Block(_source, p, SamplesPerBlock));
                    break;
                }
            }

            lock (_lock)
            {
                _blocks = newBlocks;
            }

            return true;
        }

        public int Read(AudioSourceChannelInfo info, long startPos)
        {
            var numFrames = info.NumFrames;
            var destStart = info.StartFrame;

            _nextReadPosition = startPos;

            while (numFrames > 0)
            {
                var wait = false;
                lock (_lock)
                {
                    var block = FindBlock(startPos);
                    if (block != null)
                    {
                        var offset = (int)(startPos - block.StartPos);
                        var toCopy = (int)Math.Min(numFrames, block.EndPos - startPos);
                        for (int ch = 0; ch < info.Buffer.NumberOfChannels; ch++)
                        {
                            block.AudioBuffer[ch].Offset(offset).CopyTo(info.Buffer[ch].Offset(destStart), toCopy);
                        }

                        destStart += toCopy;
                        startPos += toCopy;
                        numFrames -= toCopy;
                    }
                    else
                    {
                        wait = true;
                    }
                }

                if (wait)
                {
                    Thread.Sleep(1);
                }
            }

            return numFrames;
        }
    }
}