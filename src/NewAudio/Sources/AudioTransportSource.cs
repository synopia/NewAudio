using System.Threading;
using VL.NewAudio.Core;
using VL.NewAudio.Internal;

namespace VL.NewAudio.Sources
{
    public class AudioTransportSource : AudioSourceNode, IPositionalAudioSource
    {
        private IPositionalAudioSource? _source;

        private ResamplingAudioSource? _resamplingAudioSource;
        private BufferingAudioSource? _bufferingSource;
        private IPositionalAudioSource? _positionalSource;
        private IAudioSource? _masterSource;

        private object _lock = new();
        private bool _playing;
        private bool _stopped;
        private int _sampleRate;
        private int _sourceSampleRate;
        private int _frameSize;
        private int _readAheadBufferSize;
        private bool _isPrepared;
        private bool _inputStreamEof;
        public float Gain { get; set; } = 1.0f;

        public int SourceSampleRate
        {
            get => _sourceSampleRate;
            set => SetSource(Source, _readAheadBufferSize, value);
        }

        public IPositionalAudioSource? Source
        {
            get => _source;
            set => SetSource(value, _readAheadBufferSize, _sourceSampleRate);
        }


        public AudioTransportSource()
        {
        }

        protected override void Dispose(bool disposing)
        {
            Source = null;
            ReleaseResources();
            base.Dispose(disposing);
        }

        public int ReadAhead
        {
            get => _readAheadBufferSize;
            set => SetSource(Source, value, _sourceSampleRate);
        }

        public void SetSource(IPositionalAudioSource? newSource, int readAheadSize, int sourceSampleRateToCorrectFor,
            int maxNumChannels = 2)
        {
            if (_source == newSource)
            {
                if (_source == null)
                {
                    return;
                }

                SetSource(null, 0, 0);
            }

            _readAheadBufferSize = readAheadSize;
            _sourceSampleRate = sourceSampleRateToCorrectFor;

            ResamplingAudioSource? newResamplingSource = null;
            BufferingAudioSource? newBufferingSource = null;
            IPositionalAudioSource? newPositionalSource = null;
            IAudioSource? newMasterSource = null;

            var oldResamplingSource = _resamplingAudioSource;
            var oldBufferingSource = _bufferingSource;
            var oldMasterSource = _masterSource;

            if (newSource != null)
            {
                newPositionalSource = newSource;
                if (readAheadSize > 0)
                {
                    newPositionalSource = newBufferingSource =
                        new BufferingAudioSource(newPositionalSource, readAheadSize, maxNumChannels);
                }

                newPositionalSource.NextReadPos = 0;

                if (sourceSampleRateToCorrectFor > 0)
                {
                    newMasterSource = newResamplingSource = new ResamplingAudioSource(newPositionalSource,
                        sourceSampleRateToCorrectFor, maxNumChannels);
                }
                else
                {
                    newMasterSource = newPositionalSource;
                }

                if (_isPrepared)
                {
                    
                    newMasterSource.PrepareToPlay(_sampleRate, _frameSize);
                }
            }

            lock (_lock)
            {
                _source = newSource;
                _bufferingSource = newBufferingSource;
                _masterSource = newMasterSource;
                _positionalSource = newPositionalSource;
                _resamplingAudioSource = newResamplingSource;
                _inputStreamEof = false;
                _playing = false;
            }

            oldMasterSource?.ReleaseResources();
        }

        public void Start()
        {
            if (!_playing && _masterSource != null)
            {
                lock (_lock)
                {
                    _playing = true;
                    _stopped = false;
                    _inputStreamEof = false;
                }

                // SendChangeMessage();
            }
        }

        public void Stop()
        {
            if (_playing)
            {
                _playing = false;
                var n = 500;
                while (n-- >= 0 && !_stopped)
                {
                    Thread.Sleep(2);
                }
                // SendChangeMessage();
            }
        }

        public double Position
        {
            get => _sampleRate > 0 ? NextReadPos / (double)_sampleRate : 0.0;
            set
            {
                if (_sampleRate > 0)
                {
                    NextReadPos = (long)(_sampleRate * value);
                }
            }
        }

        public double LengthInSeconds => _sampleRate > 0 ? TotalLength / (double)_sampleRate : 0.0;
        public bool IsStreamFinished => _inputStreamEof;

        public bool IsPlaying => _playing;

        public long NextReadPos
        {
            get
            {
                if (_positionalSource != null)
                {
                    var ratio = _sampleRate > 0 && _sourceSampleRate > 0
                        ? _sampleRate / (double)_sourceSampleRate
                        : 1.0;
                    return (long)(_positionalSource.NextReadPos * ratio);
                }

                return 0;
            }
            set
            {
                if (_positionalSource != null)
                {
                    var pos = value;
                    if (_sampleRate > 0 && _sourceSampleRate > 0)
                    {
                        pos = (long)((double)pos * _sourceSampleRate / (double)_sampleRate);
                    }

                    _positionalSource.NextReadPos = pos;

                    // if()
                    _inputStreamEof = false;
                }
            }
        }

        public long TotalLength
        {
            get
            {
                lock (_lock)
                {
                    if (_positionalSource != null)
                    {
                        var ratio = _sampleRate > 0 && _sourceSampleRate > 0
                            ? _sampleRate / (double)_sourceSampleRate
                            : 1.0;
                        return (long)(_positionalSource.TotalLength * ratio);
                    }

                    return 0;
                }
            }
        }

        public bool IsLooping
        {
            get
            {
                lock (_lock)
                {
                    return _positionalSource?.IsLooping ?? false;
                }
            }
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            lock (_lock)
            {
                _sampleRate = sampleRate;
                _frameSize = framesPerBlockExpected;
                if (_masterSource != null)
                {
                    _masterSource.PrepareToPlay(sampleRate, framesPerBlockExpected);
                }

                _inputStreamEof = false;
                _isPrepared = true;
            }
        }

        public override void ReleaseResources()
        {
            lock (_lock)
            {
                if (_masterSource != null)
                {
                    _masterSource.ReleaseResources();
                }

                _isPrepared = false;
            }
        }

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            using var s = new ScopedMeasure("AudioTransportSource.GetNextAudioBlock");
            lock (_lock)
            {
                if (_masterSource != null && !_stopped)
                {
                    _masterSource.GetNextAudioBlock(bufferToFill);
                    if (!_playing)
                    {
                        if (bufferToFill.NumFrames > 256)
                        {
                            bufferToFill.Buffer.Zero(bufferToFill.StartFrame + 256, bufferToFill.NumFrames - 256);
                        }
                    }

                    if (_positionalSource!.NextReadPos > _positionalSource!.TotalLength + 1 &&
                        !_positionalSource!.IsLooping)
                    {
                        _playing = false;
                        _inputStreamEof = true;
                        // SendChangeMessage();
                    }

                    _stopped = !_playing;

                    for (var ch = 0; ch < bufferToFill.Buffer.NumberOfChannels; ch++)
                    {
                        bufferToFill.Buffer.ApplyGain(ch, bufferToFill.StartFrame, bufferToFill.NumFrames, Gain);
                    }
                }
                else
                {
                    bufferToFill.ClearActiveBuffer();
                    _stopped = true;
                }

                // lastGain = gain;
            }
        }
    }
}