using System;
using System.Threading;
using System.Threading.Tasks;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;

namespace VL.NewAudio.Sources
{
    public class BufferingAudioSource : AudioSourceNode, IPositionalAudioSource
    {
        private readonly IPositionalAudioSource _source;
        private readonly int _numberOfSamplesToBuffer;
        private readonly int _numberOfChannels;
        private readonly AudioBuffer _buffer;
        private readonly object _callbackLock = new();
        private readonly object _rangeLock = new();
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
            set
            {
                lock (_rangeLock)
                {
                    _nextPlayPos = value;
                }
            }
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


                lock (_rangeLock)
                {
                    _bufferValidStart = 0;
                    _bufferValidEnd = 0;

                    StartFillBufferTask();
                }

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

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            using var s = new ScopedMeasure("BufferingAudioSource.GetNextAudioBlock");
            var (validStart, validEnd) = GetValidBufferRange(bufferToFill.NumFrames);
            if (validEnd == validStart)
            {
                bufferToFill.ClearActiveBuffer();
                return;
            }

            lock (_callbackLock)
            {
                if (validStart > 0)
                {
                    bufferToFill.Buffer.Zero(bufferToFill.StartFrame, validStart);
                }

                if (validEnd < bufferToFill.NumFrames)
                {
                    bufferToFill.Buffer.Zero(bufferToFill.StartFrame + validEnd, bufferToFill.NumFrames - validEnd);
                }

                if (validStart < validEnd)
                {
                    for (var ch = 0; ch < Math.Min(_numberOfChannels, bufferToFill.Buffer.NumberOfChannels); ch++)
                    {
                        var startIndex =
                            (int)((validStart + _nextPlayPos) % _buffer.NumberOfFrames);
                        var endIndex =
                            (int)((validEnd + _nextPlayPos) % _buffer.NumberOfFrames);

                        if (startIndex < endIndex)
                        {
                            _buffer[ch].Offset(startIndex).CopyTo(bufferToFill.Buffer[ch]
                                .Offset(bufferToFill.StartFrame + validStart), validEnd - validStart);
                        }
                        else
                        {
                            var initialSize = _buffer.NumberOfFrames - startIndex;

                            _buffer[ch].Offset(startIndex).CopyTo(bufferToFill.Buffer[ch]
                                .Offset(bufferToFill.StartFrame + validStart), initialSize);
                            _buffer[ch].CopyTo(bufferToFill.Buffer[ch]
                                .Offset(bufferToFill.StartFrame + validStart + initialSize),
                                    validEnd - validStart - initialSize);
                        }
                    }
                }

                _nextPlayPos += bufferToFill.NumFrames;
            }
        }

        public bool WaitForNextAudioBlockReady(AudioSourceChannelInfo info, int timeout)
        {
            return false;
        }

        private (int, int) GetValidBufferRange(int numSamples)
        {
            lock (_rangeLock)
            {
                return ((int)(AudioMath.Clamp(_nextPlayPos, _bufferValidStart, _bufferValidEnd) - _nextPlayPos),
                    (int)(AudioMath.Clamp(_nextPlayPos + numSamples, _bufferValidStart, _bufferValidEnd) -
                          _nextPlayPos));
            }
        }

        private bool ReadNextBufferChunk()
        {
            long newBVS, newBVE, sectionToReadStart, sectionToReadEnd;
            lock (_rangeLock)
            {
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

            lock (_rangeLock)
            {
                _bufferValidStart = newBVS;
                _bufferValidEnd = newBVE;
            }

            _bufferReady.Set();
            return true;
        }

        private void ReadBufferSection(long start, int length, int bufferOffset)
        {
            if (_source.NextReadPos != start)
            {
                _source.NextReadPos = start;
            }

            var info = new AudioSourceChannelInfo(_buffer, bufferOffset, length);
            lock (_callbackLock)
            {
                _source.GetNextAudioBlock(info);
            }
        }
    }
}