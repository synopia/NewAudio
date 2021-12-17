using System;
using System.Collections.Generic;
using System.Diagnostics;
using VL.NewAudio.Dsp;
using Serilog;
using VL.NewAudio.Core;
using VL.NewAudio.Internal;

namespace VL.NewAudio.Backend
{
    public class XtAudioControl : IAudioControl, IAudioCallback
    {
        private readonly ILogger _logger = Resources.GetLogger<IAudioControl>();
        private readonly IAudioService _service;
        public object AudioProcessLock { get; } = new();

        private readonly List<IAudioCallback> _callbacks = new();
        private readonly CallbackHandler _callbackHandler;
        private readonly AudioBuffer _tempBuffer;

        private XtAudioSession? _currentSession;
        private bool _disposed;
        public int TotalInputChannels => _currentSession?.ActiveInputChannels.Count ?? 0;
        public int TotalOutputChannels => _currentSession?.ActiveOutputChannels.Count ?? 0;

        public bool IsRunning => _currentSession?.IsRunning ?? false;

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
                _tempBuffer.Dispose();
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

        public IAudioSession? Open(AudioStreamBuilder builder)
        {
            Close();
            _logger.Information("Opening audio control ({@Builder})", builder);

            _currentSession = new XtAudioSession(builder.Build());
            _currentSession.Start(_callbackHandler);
            return _currentSession;
        }


        public void AddAudioCallback(IAudioCallback callback)
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
                callback.OnAudioWillStart(_currentSession);
            }

            lock (AudioProcessLock)
            {
                _callbacks.Add(callback);
            }
        }

        public void RemoveAudioCallback(IAudioCallback callback)
        {
            var stopped = _currentSession != null;
            lock (AudioProcessLock)
            {
                stopped &= _callbacks.Contains(callback);
                _callbacks.Remove(callback);
            }

            if (stopped)
            {
                callback.OnAudioStopped();
            }
        }


        public void OnAudio(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            using var s = new ScopedMeasure("XtAudioControl.OnAudio");
            lock (AudioProcessLock)
            {
                if (_callbacks.Count > 0)
                {
                    _tempBuffer.SetSize(Math.Max(1, output.NumberOfChannels), Math.Max(1, numFrames), false, false,
                        true);
                    _callbacks[0].OnAudio(input, output, numFrames);
                    for (var i = 1; i<_callbacks.Count; i++)
                    {
                        _callbacks[i].OnAudio(input, _tempBuffer, numFrames);

                        for (var ch = 0; ch < output.NumberOfChannels; ch++)
                        {
                            output[ch].Add(_tempBuffer[ch], numFrames);
                        }
                    }
                }
                else
                {
                    output.Zero();
                }
            }
        }

        public void OnAudioWillStart(IAudioSession session)
        {
            lock (AudioProcessLock)
            {
                for (var i = _callbacks.Count; --i >= 0;)
                {
                    _callbacks[i].OnAudioWillStart(session);
                }
            }

            UpdateCurrentSetup();
            SendChangeMessage();
        }

        private void UpdateCurrentSetup()
        {
        }

        public void OnAudioStopped()
        {
            SendChangeMessage();
            lock (AudioProcessLock)
            {
                for (var i = _callbacks.Count; --i >= 0;)
                {
                    _callbacks[i].OnAudioStopped();
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


        public void OnAudioError(string errorMessage)
        {
            Trace.WriteLine(errorMessage);
        }
    }
}