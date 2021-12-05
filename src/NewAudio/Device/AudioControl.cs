using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewAudio.Dispatcher;
using NewAudio.Dsp;
using Serilog;
using Xt;

namespace NewAudio.Device
{
    public interface IAudioControl : IChangeBroadcaster, IDisposable
    {
        // void OpenAudioDeviceSetup(DeviceConfig[] setup);
        // void OpenAudioDeviceConfig(DeviceConfig config);

        void AddAudioCallback(IAudioDeviceCallback callback);
        void RemoveAudioCallback(IAudioDeviceCallback callback);

        double GetCpuUsage();

        object AudioProcessLock { get; }

        void PlayTestSound(int index);

        int XRunCount { get; }

        IAudioSession Open(AudioStreamConfig primary, AudioStreamConfig[]? secondary);

        void Close();
        bool IsRunning { get; }
    }

    public class CallbackHandler : IAudioDeviceCallback
    {
        private IAudioDeviceCallback _owner;

        public CallbackHandler(IAudioDeviceCallback owner)
        {
            _owner = owner;
        }

        public void AudioDeviceCallback(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            _owner.AudioDeviceCallback(input, output, numFrames);
        }

        public void AudioDeviceAboutToStart(IAudioSession session)
        {
            _owner.AudioDeviceAboutToStart(session);
        }

        public void AudioDeviceStopped()
        {
            _owner.AudioDeviceStopped();
        }

        public void AudioDeviceError(string errorMessage)
        {
            _owner.AudioDeviceError(errorMessage);
        }
    }

    public class XtAudioControl : IAudioControl, IAudioDeviceCallback
    {
        private readonly IAudioService _service;
        public object AudioProcessLock { get; } = new();
        
        private readonly List<IAudioDeviceCallback> _callbacks = new();
        private readonly CallbackHandler _callbackHandler;
        private readonly AudioBuffer _tempBuffer;

        private AudioSession? _currentSession;
        private bool _disposed;

        public bool IsRunning => _currentSession?.IsRunning ?? false;
        private ILogger _logger = Resources.GetLogger<IAudioControl>();

        public XtAudioControl(IAudioService service)
        {
            _callbackHandler = new CallbackHandler(this);
            _tempBuffer = new AudioBuffer();

            _service = service;
            _logger.Information("Audio control created: {@This}", this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.Information("Audio control disposed {@This}", this);
                _disposed = true;
                Close();
            }
        }

        public void PlayTestSound(int index)
        {
        }

        public void Close()
        {
            _currentSession?.Dispose();
            _currentSession = null;
        }

        public IAudioSession Open(AudioStreamConfig primary, AudioStreamConfig[]? secondary)
        {
            Close();
            _logger.Information("Opening audio control (primary={@Primary}, secondary={@Secondary})", primary, secondary);

            var stream = primary.CreateStream(secondary);
            
            _currentSession = new AudioSession(stream);
            _currentSession.Start(_callbackHandler);

            return _currentSession;
        }

        
        public void AddAudioCallback(IAudioDeviceCallback callback)
        {
            lock (AudioProcessLock)
            {
                if (_callbacks.Contains(callback))
                {
                    return;
                }
            }

            if (_currentSession != null)
            {
                callback.AudioDeviceAboutToStart(_currentSession);
            }

            lock (AudioProcessLock)
            {
                _callbacks.Add(callback);
            }
        }

        public void RemoveAudioCallback(IAudioDeviceCallback callback)
        {
            bool needReinit = _currentSession != null;
            lock (AudioProcessLock)
            {
                needReinit = needReinit && _callbacks.Contains(callback);
                _callbacks.Remove(callback);
            }

            if (needReinit)
            {
                callback.AudioDeviceStopped();
            }
        }


        public void AudioDeviceCallback(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            lock (AudioProcessLock)
            {
                if (_callbacks.Count > 0)
                {
                    _tempBuffer.SetSize(Math.Max(1, output.NumberOfChannels), Math.Max(1, numFrames), false, false,
                        true);
                    _callbacks[0].AudioDeviceCallback(input, output, numFrames);
                    for (int i = _callbacks.Count; --i > 0;)
                    {
                        _callbacks[i].AudioDeviceCallback(input, _tempBuffer, numFrames);

                        for (int ch = 0; ch < output.NumberOfChannels; ch++)
                        {
                            output[ch].Span.Add(_tempBuffer[0].Span, numFrames);
                        }
                    }
                }
                else
                {
                    output.Zero();
                }
            }
        }

        public void AudioDeviceAboutToStart(IAudioSession session)
        {
            lock (AudioProcessLock)
            {
                for (int i = _callbacks.Count; --i >= 0;)
                {
                    _callbacks[i].AudioDeviceAboutToStart(session);
                }
            }

            UpdateCurrentSetup();
            SendChangeMessage();
        }

        private void UpdateCurrentSetup()
        {
        }

        public void AudioDeviceStopped()
        {
            SendChangeMessage();
            lock (AudioProcessLock)
            {
                for (int i = _callbacks.Count; --i >= 0;)
                {
                    _callbacks[i].AudioDeviceStopped();
                }
            }
        }

        public void SendChangeMessage()
        {
        }

        public double GetCpuUsage()
        {
            return 0.0;
        }

        public int XRunCount => 0;

        public void AudioDeviceError(string errorMessage)
        {
            Trace.WriteLine(errorMessage);
        }
    }
}