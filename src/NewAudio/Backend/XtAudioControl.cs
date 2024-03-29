﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using VL.NewAudio.Dsp;
using Serilog;
using VL.NewAudio.Core;

namespace VL.NewAudio.Backend
{
    public class XtAudioControl : IAudioControl, IAudioDeviceCallback
    {
        private readonly ILogger _logger = Resources.GetLogger<IAudioControl>();
        private readonly IAudioService _service;
        public object AudioProcessLock { get; } = new();

        private readonly List<IAudioDeviceCallback> _callbacks = new();
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
            var needReinit = _currentSession != null;
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
                    for (var i = _callbacks.Count; --i > 0;)
                    {
                        _callbacks[i].AudioDeviceCallback(input, _tempBuffer, numFrames);

                        for (var ch = 0; ch < output.NumberOfChannels; ch++)
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
                for (var i = _callbacks.Count; --i >= 0;)
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
                for (var i = _callbacks.Count; --i >= 0;)
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


        public void AudioDeviceError(string errorMessage)
        {
            Trace.WriteLine(errorMessage);
        }
    }
}