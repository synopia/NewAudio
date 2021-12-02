using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using NewAudio.Device;
using NewAudio.Dsp;
using Xt;

namespace NewAudio.Device
{
    public interface IAudioSession: IDisposable
    {
        void Start();
        void Stop();
        int CurrentSampleRate { get; }
        int CurrentFramesPerBlock { get;  }
        AudioChannels ActiveInputChannels { get; }
        AudioChannels ActiveOutputChannels { get; }
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
        private AudioStreamConfig? InputConfig;
        private AudioStreamConfig OutputConfig;

        public int CurrentSampleRate => OutputConfig.SampleRate;
        public AudioChannels ActiveInputChannels => InputConfig?.ActiveChannels ?? AudioChannels.Disabled;
        public AudioChannels ActiveOutputChannels => OutputConfig.ActiveChannels;

        private IAudioStream? _inputStream;
        private IAudioStream _outputStream;

        private int _audioCallbackGuard;
        public double InputLatency { get; private set; }
        public double OutputLatency { get; private set; }
        public int XRuns { get; private set; }
        public int CurrentFramesPerBlock => _outputStream.FramesPerBlock;
        private IAudioDeviceCallback? _currentCallback;
        private bool _running;
        private AudioStreamType _type;
        public bool IsRunning => _running;

        public AudioSession(IAudioDevice? inputDevice, IAudioDevice outputDevice)
        {
            InputConfig = inputDevice?.GetConfig(true);
            OutputConfig = outputDevice.GetConfig(false);

            Trace.Assert(OutputConfig.ActiveChannels.Count > 0);
            
            (_type, _outputStream, _inputStream) = OutputConfig.CreateStream(InputConfig, this, new CaptureCallback());
        }

        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _outputStream.Dispose();
                _inputStream?.Dispose();

                _inputStream = null;
            }
        }

        
        public void Start(IAudioDeviceCallback? callback)
        {
            Trace.Assert(_outputStream!=null);
            
            if (callback != _currentCallback)
            {
                if (callback != null)
                {
                    callback.AudioDeviceAboutToStart(this);
                }

                var old = _currentCallback;

                if (old != null)
                {
                    if (callback == null)
                    {
                        Stop();
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
                    _running = true;
                    Start();
                }
                _currentCallback = callback;
            }
        }
        
        private void SetCallback(IAudioDeviceCallback? callback)
        {
            if (!_running)
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

        public void Process(AudioBuffer input, AudioBuffer output, int numFrames)
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
            
            _inputStream?.Start();
            _outputStream.Start();
        }

        public void Stop()
        {
            while (Interlocked.CompareExchange(ref _audioCallbackGuard, 1, 0) == 1)
            {
                Thread.Sleep(1);
            }
            _inputStream?.Dispose();
            _outputStream.Dispose();
            
            _inputStream = null;
            _audioCallbackGuard = 0;
        }


        public int OnBuffer(XtStream stream, in XtBuffer buffer, object user)
        {
            if (Interlocked.CompareExchange(ref _audioCallbackGuard, 1, 0) == 0)
            {
                try
                {
                    Trace.Assert(stream != null && buffer.output != null);
                    Process(_inputStream?.Bind(buffer), _outputStream.Bind(buffer), buffer.frames);

                    var latency = _outputStream.Latency;
                    OutputLatency = latency.output;
                    InputLatency = latency.input;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
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
            Console.WriteLine($"OnRunning {running}, {error}");
        }

        
    }
}