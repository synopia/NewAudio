﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VL.NewAudio.Dsp;
using Serilog;
using VL.NewAudio.Core;
using VL.NewAudio.Internal;
using Xt;

namespace VL.NewAudio.Backend
{
    public class XtAudioSession : IAudioSession, IAudioStreamCallback
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

        private AudioStreamConfig Config => _stream.Config;
        private readonly ILogger _logger = Resources.GetLogger<XtAudioSession>();

        public int CurrentSampleRate => Config.SampleRate;
        public AudioChannels ActiveInputChannels => AudioChannels.Channels(_stream.NumInputChannels);
        public AudioChannels ActiveOutputChannels => AudioChannels.Channels(_stream.NumOutputChannels);

        private readonly IAudioInputOutputStream _stream;

        private int _audioCallbackGuard;
        public double InputLatency => _stream.InputLatency;
        public double OutputLatency => _stream.OutputLatency;
        private int _xRuns;
        public int XRuns => LoadMeasure.XRuns + _xRuns; 
        public int CurrentFramesPerBlock => _stream.FramesPerBlock;
        private IAudioCallback? _currentCallback;
        public AudioStreamType Type => _stream.Type;
        public bool IsRunning { get; private set; }
        public double CpuUsage => LoadMeasure.CpuUsage * 100;
        public IEnumerable<string> Times => LoadMeasure.Stack.Select(e=>$"{e.Item1} ({e.Item2})");

        public XtAudioSession(IAudioInputOutputStream stream)
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
                Stop();
            }
        }

        public void Start(IAudioCallback? callback)
        {
            Trace.Assert(_stream != null);
            _logger.Information("Session start");

            if (callback != _currentCallback)
            {
                callback?.OnAudioWillStart(this);

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

                    old.OnAudioStopped();
                }
                else
                {
                    Trace.Assert(callback != null);
                    SetCallback(callback);
                    Start();
                }

                _currentCallback = callback;
            }
        }

        private void SetCallback(IAudioCallback? callback)
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

                if (old != null && old == Interlocked.CompareExchange(ref _currentCallback, callback, old))
                {
                    break;
                }

                Thread.Sleep(1);
            }
        }

        private void Process(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            var cb = _currentCallback;
            // var cb = Interlocked.Exchange(ref _currentCallback, null);
            if (cb != null)
            {
                using var s = new ScopedMeasure("XtAudioSession.Process");
                cb.OnAudio(input, output, numFrames);
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

        public void Stop()
        {
            // while (Interlocked.CompareExchange(ref _audioCallbackGuard, 1, 0) == 0)
            // {
                // Thread.Sleep(1);
            // }

            _logger.Information("AudioSession closing stream");
            _stream.Stop();
            _stream.Dispose();
            _audioCallbackGuard = 0;
        }

        
        public int OnBuffer(XtStream stream, in XtBuffer buffer, object user)
        {
            // if (Interlocked.CompareExchange(ref _audioCallbackGuard, 1, 0) == 0)
            // {
                try
                {
                    LoadMeasure.Start(buffer.frames / (double)CurrentSampleRate * 1000.0);
                    
                    Trace.Assert(stream != null && buffer.output != null);
                    var inputBuffer = _stream.BindInput(buffer);
                    var outputBuffer = _stream.BindOutput(buffer);
                    Process(inputBuffer, outputBuffer, buffer.frames);

                    LoadMeasure.Stop();
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Exception in AudioSession.OnBuffer!");
                    _currentCallback?.OnAudioError(e.Message);
                }
                finally
                {
                    _audioCallbackGuard = 0;
                }
            // }

            return 0;
        }

        public void OnXRun(XtStream stream, int index, object user)
        {
            _logger.Warning("XRun! {Cnt}", XRuns);
            _xRuns++;
        }

        public void OnRunning(XtStream stream, bool running, ulong error, object user)
        {
            _logger.Information("AudioStream.OnRunning: running={Running}, error={Error}", running, error);
            if (error != 0)
            {
                _currentCallback?.OnAudioError(error.ToString());
            }

            IsRunning = running;
        }
    }
}