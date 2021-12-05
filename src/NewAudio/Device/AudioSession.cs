using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using NewAudio.Device;
using NewAudio.Dsp;
using Serilog;
using Xt;

namespace NewAudio.Device
{
    public interface IAudioSession: IDisposable
    {
        void Start();
        void Close();
        int CurrentSampleRate { get; }
        int CurrentFramesPerBlock { get;  }
        AudioChannels ActiveInputChannels { get; }
        AudioChannels ActiveOutputChannels { get; }
        
        AudioStreamType Type { get; }
        double InputLatency { get; }
        double OutputLatency { get; }
    }

    public class AudioSession : IAudioSession, IAudioStreamCallback
    {
        private class CaptureCallback : IAudioStreamCallback
        {
            public int OnBuffer(XtStream stream, in XtBuffer buffer, object user)
            {
                throw new NotImplementedException();
            }

            public void OnXRun(XtStream stream, int index, object user)
            {
                throw new NotImplementedException();
            }

            public void OnRunning(XtStream stream, bool running, ulong error, object user)
            {
                throw new NotImplementedException();
            }
        }
        // private XtAudioDevice _owner;
        private AudioStreamConfig Config => _stream.Config;

        public int CurrentSampleRate => Config.SampleRate;
        public AudioChannels ActiveInputChannels => AudioChannels.Channels(_stream.NumInputChannels);
        public AudioChannels ActiveOutputChannels => AudioChannels.Channels(_stream.NumOutputChannels);

        private readonly IAudioStream _stream;

        private int _audioCallbackGuard;
        public double InputLatency { get; private set; }
        public double OutputLatency { get; private set; }
        public int XRuns { get; private set; }
        public int CurrentFramesPerBlock => _stream.FramesPerBlock;
        private IAudioDeviceCallback? _currentCallback;
        public AudioStreamType Type => _stream.Type;
        public bool IsRunning { get; private set; }
        private ILogger _logger = Resources.GetLogger<AudioSession>();

        public AudioSession(IAudioStream stream)
        {
            _stream = stream;

            if (_stream.NumOutputChannels <= 0)
            {
                throw new InvalidOperationException("At least one output channel is needed!");
            }
            _logger.Information("Opening session for stream {@Config}", stream.Config);
            _stream.Open(this);
        }

        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.Information("Disposing session for stream {@Config}", _stream.Config);
                _disposed = true;
                _stream.Dispose();
            }
        }

        
        public void Start(IAudioDeviceCallback? callback)
        {
            Trace.Assert(_stream!=null);
            _logger.Information("Session start");
      
            if (callback != _currentCallback)
            {
                callback?.AudioDeviceAboutToStart(this);

                var old = _currentCallback;

                if (old != null)
                {
                    if (callback == null)
                    {
                        Close();
                    }
                    else
                    {
                        SetCallback(callback);   
                    }
                
                    old.AudioDeviceStopped();
                }
                else
                {
                    Trace.Assert(callback!=null);
                    SetCallback(callback);
                    Start();
                }
                _currentCallback = callback;
            }
        }
        
        private void SetCallback(IAudioDeviceCallback? callback)
        {
            if (!IsRunning)
            {
                _currentCallback = callback;
                return;
            }

            while (true)
            {
                var old = _currentCallback;
                if (old == callback)
                {
                    break;
                }
                if( old!=null && old==Interlocked.CompareExchange(ref _currentCallback, callback, old)){
                    break;
                }
                Thread.Sleep(1);
            }
        }

        public void Process(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            var cb = Interlocked.Exchange(ref _currentCallback, null);
            if (cb != null)
            {
                cb.AudioDeviceCallback(input, output, numFrames);
                _currentCallback = cb;
            }
            else
            {
                output.Zero();
            }
        }

        
        
        public void Start()
        {
            _audioCallbackGuard = 0;
            
            _stream.Start();
            _logger.Information("AudioSession stream started");
        }

        public void Close()
        {
            while (Interlocked.CompareExchange(ref _audioCallbackGuard, 1, 0) == 1)
            {
                Thread.Sleep(1);
            }
            _logger.Information("AudioSession closing stream");
            _stream.Dispose();
            _audioCallbackGuard = 0;
        }


        public int OnBuffer(XtStream stream, in XtBuffer buffer, object user)
        {
            if (Interlocked.CompareExchange(ref _audioCallbackGuard, 1, 0) == 0)
            {
                try
                {
                    Trace.Assert(stream != null && buffer.output != null);
                    var inputBuffer = _stream.BindInput(buffer);
                    var outputBuffer = _stream.BindOutput(buffer);
                    Process(inputBuffer, outputBuffer, buffer.frames);

                    var latency = _stream.Latency;
                    OutputLatency = latency.output;
                    InputLatency = latency.input;
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Exception in AudioSession.OnBuffer!");
                }
                finally
                {
                    _audioCallbackGuard = 0;
                }

            }

            return 0;
        }

        public void OnXRun(XtStream stream, int index, object user)
        {
            XRuns++;
        }

        public void OnRunning(XtStream stream, bool running, ulong error, object user)
        {
            _logger.Information("AudioStream.OnRunning: running={Running}, error={Error}", running, error);
            IsRunning = running;
        }

        
    }
}