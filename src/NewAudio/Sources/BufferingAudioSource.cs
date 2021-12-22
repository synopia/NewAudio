using System;
using System.Threading;
using System.Threading.Tasks;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;

namespace VL.NewAudio.Sources
{
    public class BufferingAudioSource : AudioSourceBase, IPositionalAudioSource
    {
        private readonly IPositionalAudioSource _source;
        private readonly int _numberOfSamplesToBuffer;
        private readonly int _numberOfChannels;
        private readonly AudioBuffer _buffer;
        private readonly ManualResetEvent _bufferReady;
        private long _bufferValidStart;
        private long _bufferValidEnd;
        private long _nextPlayPos;
        private int _sampleRate;
        private bool _wasSourceLooping;
        private bool _isPrepared;
        private bool _prefill;
        private CancellationTokenSource _cts = new();

        public long NextReadPos
        {
            get =>
                _source.IsLooping && _nextPlayPos > 0
                    ? _nextPlayPos % _source.TotalLength
                    : _nextPlayPos;
            set { _nextPlayPos = value; }
        }

        private Task? _fillBufferTask;

        public long TotalLength => _source.TotalLength;
        public bool IsLooping => _source.IsLooping;

        public BufferingAudioSource(IPositionalAudioSource source, int numberOfSamplesToBuffer, int numberOfChannels,
            bool prefill = true)
        {
            _source = source;
            _numberOfSamplesToBuffer = Math.Max(1024, numberOfSamplesToBuffer);
            _numberOfChannels = numberOfChannels;
            _prefill = prefill;
            _buffer = new AudioBuffer();
            _bufferReady = new ManualResetEvent(false);
        }

        private void StartFillBufferTask()
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

        private void StopFillBufferTask()
        {
            if (_fillBufferTask != null)
            {
                _cts.Cancel();
                _fillBufferTask.GetAwaiter().GetResult();
            }

            _fillBufferTask = null;
        }

        protected override void Dispose(bool disposing)
        {
            _cts.Cancel();
            _bufferReady.Dispose();
            _buffer.Dispose();
            ReleaseResources();
            base.Dispose(disposing);
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            var bufferSizeNeeded = Math.Max(framesPerBlockExpected * 2, _numberOfSamplesToBuffer);
            if (sampleRate != _sampleRate || bufferSizeNeeded != _buffer.NumberOfFrames || !_isPrepared)
            {
                _isPrepared = true;
                _sampleRate = sampleRate;
                _source.PrepareToPlay(sampleRate, framesPerBlockExpected);
                _buffer.SetSize(_numberOfChannels, bufferSizeNeeded);
                _buffer.Zero();


                _bufferValidStart = 0;
                _bufferValidEnd = 0;

                StartFillBufferTask();

                do
                {
                    Thread.Sleep(5);
                } while (_prefill && (int)(_bufferValidEnd - _bufferValidStart) <
                         Math.Min(sampleRate / 4, _buffer.NumberOfFrames / 2));
            }
        }

        public override void ReleaseResources()
        {
            _isPrepared = false;
            StopFillBufferTask();
            _buffer.SetSize(_numberOfChannels, 0);
            _source.ReleaseResources();
        }

        public override void FillNextBuffer(AudioBufferToFill buffer)
        {
            using var s = new ScopedMeasure("BufferingAudioSource.GetNextAudioBlock");
            var (validStart, validEnd) = GetValidBufferRange(buffer.NumFrames);
            if (validEnd == validStart)
            {
                buffer.ClearActiveBuffer();
                return;
            }

            if (validStart > 0)
            {
                buffer.Buffer.Zero(buffer.StartFrame, validStart);
            }

            if (validEnd < buffer.NumFrames)
            {
                buffer.Buffer.Zero(buffer.StartFrame + validEnd, buffer.NumFrames - validEnd);
            }

            if (validStart < validEnd)
            {
                for (var ch = 0; ch < Math.Min(_numberOfChannels, buffer.Buffer.NumberOfChannels); ch++)
                {
                    var startIndex =
                        (int)((validStart + _nextPlayPos) % _buffer.NumberOfFrames);
                    var endIndex =
                        (int)((validEnd + _nextPlayPos) % _buffer.NumberOfFrames);

                    if (startIndex < endIndex)
                    {
                        _buffer[ch].Offset(startIndex).CopyTo(buffer.Buffer[ch]
                            .Offset(buffer.StartFrame + validStart), validEnd - validStart);
                    }
                    else
                    {
                        var initialSize = _buffer.NumberOfFrames - startIndex;

                        _buffer[ch].Offset(startIndex).CopyTo(buffer.Buffer[ch]
                            .Offset(buffer.StartFrame + validStart), initialSize);
                        _buffer[ch].CopyTo(buffer.Buffer[ch]
                                .Offset(buffer.StartFrame + validStart + initialSize),
                            validEnd - validStart - initialSize);
                    }
                }

                _nextPlayPos += buffer.NumFrames;
            }
        }

        public bool WaitForNextAudioBlockReady(AudioBufferToFill info, int timeout)
        {
            return false;
        }

        private (int, int) GetValidBufferRange(int numSamples)
        {
            return ((int)(AudioMath.Clamp(_nextPlayPos, _bufferValidStart, _bufferValidEnd) - _nextPlayPos),
                (int)(AudioMath.Clamp(_nextPlayPos + numSamples, _bufferValidStart, _bufferValidEnd) -
                      _nextPlayPos));
        }

        private bool ReadNextBufferChunk()
        {
            long newBVS, newBVE, sectionToReadStart, sectionToReadEnd;
            if (_wasSourceLooping != IsLooping)
            {
                _wasSourceLooping = IsLooping;
                _bufferValidStart = 0;
                _bufferValidEnd = 0;
            }

            newBVS = Math.Max(0, _nextPlayPos);
            newBVE = newBVS + _buffer.NumberOfFrames - 4;
            sectionToReadStart = 0;
            sectionToReadEnd = 0;
            const int maxChunkSize = 2048;
            if (newBVS < _bufferValidStart || newBVS >= _bufferValidEnd)
            {
                newBVE = Math.Min(newBVE, newBVS + maxChunkSize);
                sectionToReadStart = newBVS;
                sectionToReadEnd = newBVE;
                _bufferValidStart = 0;
                _bufferValidEnd = 0;
            }
            else if (Math.Abs((int)(newBVS - _bufferValidStart)) > 512
                     || Math.Abs((int)(newBVE - _bufferValidEnd)) > 512)
            {
                newBVE = Math.Min(newBVE, _bufferValidEnd + maxChunkSize);
                sectionToReadStart = _bufferValidEnd;
                sectionToReadEnd = newBVE;
                _bufferValidStart = newBVS;
                _bufferValidEnd = Math.Min(_bufferValidEnd, newBVE);
            }

            if (sectionToReadStart == sectionToReadEnd)
            {
                return false;
            }

            var bufferIndexStart = (int)(sectionToReadStart % _buffer.NumberOfFrames);
            var bufferIndexEnd = (int)(sectionToReadEnd % _buffer.NumberOfFrames);
            if (bufferIndexStart < bufferIndexEnd)
            {
                ReadBufferSection(sectionToReadStart, (int)(sectionToReadEnd - sectionToReadStart), bufferIndexStart);
            }
            else
            {
                var initialSize = _buffer.NumberOfFrames - bufferIndexStart;
                ReadBufferSection(sectionToReadStart, initialSize, bufferIndexStart);
                ReadBufferSection(sectionToReadStart + initialSize,
                    (int)(sectionToReadEnd - sectionToReadStart) - initialSize, 0);
            }

            _bufferValidStart = newBVS;
            _bufferValidEnd = newBVE;

            _bufferReady.Set();
            return true;
        }

        private void ReadBufferSection(long start, int length, int bufferOffset)
        {
            if (_source.NextReadPos != start)
            {
                _source.NextReadPos = start;
            }

            var info = new AudioBufferToFill(_buffer, bufferOffset, length);
            _source.FillNextBuffer(info);
        }
    }
}