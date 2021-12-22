using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;

namespace VL.NewAudio.Files
{
    internal record Block
    {
        private ILogger _logger = Resources.GetLogger<AudioFileBufferedReader>();
        public Block(IAudioFileReader reader, long startPos, int length)
        {
            StartPos = startPos;
            EndPos = Math.Min(StartPos + length, reader.Samples);
            length = (int)(EndPos - StartPos);
            AudioBuffer = new AudioBuffer(reader.Channels, length);
            reader.Read(new AudioBufferToFill(AudioBuffer, 0, length), startPos);
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
        private ILogger _logger = Resources.GetLogger<AudioFileBufferedReader>();
        
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
        private bool _disposedValue;
        private Stopwatch _timer = new();
        private double _timeOut;
        private readonly object _lock = new();

        public AudioFileBufferedReader(IAudioFileReader source, int samplesToBuffer, double timeOut = 50.0)
        {
            _source = source;
            _numBlocks = 1 + samplesToBuffer / SamplesPerBlock;
            _timeOut = timeOut;
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
                    try
                    {
                        var r = ReadNextBufferChunk();
                        Thread.Sleep(TimeSpan.FromMilliseconds(r ? 1 : 100));
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e,"StartFillBlockTask");
                    }
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
            foreach (var block in _blocks)
            {
                if (block.StartPos==0 || block.Intersects(pos, endPos))
                {
                    newBlocks.Add(block);
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

        public void Read(AudioBufferToFill info, long startPos)
        {
            var numFrames = info.NumFrames;
            var destStart = info.StartFrame;

            _nextReadPosition = startPos;
            _timer.Restart();
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
                    if (_timer.Elapsed.TotalMilliseconds > _timeOut)
                    {
                        for (int ch = 0; ch < info.Buffer.NumberOfChannels; ch++)
                        {
                            info.Buffer[ch].Zero(destStart, numFrames);
                        }

                        break;
                    }

                    Thread.Sleep(1);
                }
            }
        }
    }
}