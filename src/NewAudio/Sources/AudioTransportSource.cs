using System.Threading;
using VL.NewAudio.Core;

namespace VL.NewAudio.Sources
{
    public class AudioTransportSource: AudioSourceNode, IPositionalAudioSource
    {
        
        private IPositionalAudioSource? _source;
        // Resampling
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
        private bool _inputStreamEOF;
        public float Gain { get; set; } = 1.0f;

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
            ReleaseMasterSource();
            base.Dispose(disposing);
        }

        public void SetSource(IPositionalAudioSource? newSource, int readAheadSize, int sourceSampleRateToCorrectFor, int maxNumChannels=2)
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

            BufferingAudioSource? newBufferingSource = null;
            IPositionalAudioSource? newPositionalSource = null;
            IAudioSource? newMasterSource = null;

            BufferingAudioSource? oldBufferingSource = _bufferingSource;
            IAudioSource? oldMasterSource = _masterSource;

            if (newSource != null)
            {
                newPositionalSource = newSource;
                if (readAheadSize > 0)
                {
                    newPositionalSource = newBufferingSource =
                        new BufferingAudioSource(newPositionalSource, readAheadSize, maxNumChannels);
                }

                newPositionalSource.NextReadPos = 0;
                // if (sourceSampleRateToCorrectFor > 0)
                // {
                    // newMasterSource
                // }
                newMasterSource = newPositionalSource;

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
                _inputStreamEOF = false;
                _playing = false;
                
            }

            if (oldMasterSource != null)
            {
                oldMasterSource.ReleaseResources();
            }
        }
        
        
        public void Start()
        {
            if (!_playing && _masterSource != null)
            {
                lock (_lock)
                {
                    _playing = true;
                    _stopped = false;
                    _inputStreamEOF = false;
                }

                // SendChangeMessage();
            }
        }

        public void Stop()
        {
            if (_playing)
            {
                _playing = false;
                int n = 500;
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
                    NextReadPos = (ulong)(_sampleRate * value);
                }
            }
        }

        public double LengthInSeconds => _sampleRate > 0 ? TotalLength / (double)_sampleRate : 0.0;
        public bool IsStreamFinished { get; }

        public bool IsPlaying { get; }

        public ulong NextReadPos
        {
            get
            {
                if (_positionalSource != null)
                {
                    var ratio = (_sampleRate > 0 && _sourceSampleRate > 0)
                        ? _sampleRate / (double)_sourceSampleRate
                        : 1.0;
                    return (ulong)(_positionalSource.NextReadPos * ratio);
                }

                return 0;
            }
            set
            {
                if (_positionalSource != null)
                {
                    ulong pos = value;
                    if (_sampleRate > 0 && _sourceSampleRate > 0)
                    {
                        pos =(ulong) ((double)pos * _sourceSampleRate / (double)_sampleRate);
                    }

                    _positionalSource.NextReadPos = pos;
                    
                    // if()
                    _inputStreamEOF = false;
                }
            }
        }

        public ulong TotalLength
        {
            get
            {
                lock (_lock)
                {
                    if (_positionalSource != null)
                    {
                        var ratio = (_sampleRate > 0 && _sourceSampleRate > 0)
                            ? _sampleRate / (double)_sourceSampleRate
                            : 1.0;
                        return (ulong)(_positionalSource.NextReadPos * ratio);
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

                _inputStreamEOF = false;
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
            lock (_lock)
            {
                if (_masterSource != null && !_stopped)
                {
                    _masterSource.GetNextAudioBlock(bufferToFill);
                    if (!_playing)
                    {
                        if (bufferToFill.NumFrames > 256)
                        {
                            bufferToFill.Buffer.Zero(bufferToFill.StartFrame+256, bufferToFill.NumFrames-256);
                        }
                    }

                    if (_positionalSource!.NextReadPos > _positionalSource!.TotalLength + 1 && !_positionalSource!.IsLooping)
                    {
                        _playing = false;
                        _inputStreamEOF = true;
                        // SendChangeMessage();
                    }

                    _stopped = !_playing;
                }
                else
                {
                    bufferToFill.ClearActiveBuffer();
                    _stopped = true;
                }

                // lastGain = gain;
            }
        }

        
        private void ReleaseMasterSource()
        {
            
        }
    }
}