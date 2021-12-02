using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewAudio.Dispatcher;
using NewAudio.Dsp;
using Xt;

namespace NewAudio.Device
{
    public interface IAudioPlatform : IChangeBroadcaster, IDisposable
    {
        // void OpenAudioDeviceSetup(DeviceConfig[] setup);
        // void OpenAudioDeviceConfig(DeviceConfig config);

        void AddAudioCallback(IAudioDeviceCallback callback);
        void RemoveAudioCallback(IAudioDeviceCallback callback);

        double GetCpuUsage();

        object AudioProcessLock { get; }

        void PlayTestSound(int index);

        int XRunCount { get; }
        IAudioSession Open(string inputDeviceId,string outputDeviceId, int numInputs, int numOutputs);
    }

    public class CallbackHandler : IAudioDeviceCallback
    {
        private IAudioDeviceCallback _owner;

        public CallbackHandler(IAudioDeviceCallback owner)
        {
            _owner = owner;
        }

        public void AudioDeviceCallback(AudioBuffer input, AudioBuffer output, int numFrames)
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

    public class XtAudioPlatform : IAudioPlatform, IAudioDeviceCallback
    {
        private XtPlatform _platform;
        private CallbackHandler _callbackHandler;
        public object AudioProcessLock { get; } = new();
        private List<IAudioDeviceCallback> _callbacks = new();
        private AudioBuffer _tempBuffer;

        private IAudioService _service;
        private bool _scannedForDevices = false;
        private AudioSession? _currentSession;

        public XtAudioPlatform(XtPlatform platform)
        {
            _platform = platform;
            _callbackHandler = new CallbackHandler(this);
            _tempBuffer = new AudioBuffer();

            _service = new XtAudioService(platform);
        }

        public void Dispose()
        {
            _currentSession?.Dispose();
            _service.Dispose();
            _platform.Dispose();
        }

        public void PlayTestSound(int index)
        {
        }

        private void ScanIfNeeded()
        {
            if (!_scannedForDevices)
            {
                _service.ScanForDevices();
                _scannedForDevices = true;
            }
        }

        public void Close()
        {
            if (_currentSession != null)
            {
                _currentSession.Stop();
            }
        }

        public IAudioSession Open(string inputDeviceId,string outputDeviceId, int numInputs, int numOutputs)
        {
            Close();

            ScanIfNeeded();

            var output = _service.OpenDevice(outputDeviceId); 
            var input = inputDeviceId!=outputDeviceId ? _service.OpenDevice(inputDeviceId) : output;

            numInputs = Math.Min(numInputs, input.InputChannelNames.Length);
            numOutputs = Math.Min(numOutputs, output.OutputChannelNames.Length);

            // var sampleType = ChooseBestSampleType(_currentDevice, XtSample.Float32);
            var sampleRate = ChooseBestSampleRate(output, 0);
            var bufferSize = ChooseBestBufferSize(output);

            
            if (outputDeviceId != inputDeviceId)
            {
                output.Open(AudioChannels.Disabled, AudioChannels.Channels(numOutputs),
                    sampleRate,
                    bufferSize);
                input.Open(AudioChannels.Channels(numInputs), AudioChannels.Disabled,
                    sampleRate,
                    bufferSize);
            }
            else
            {
                output.Open(AudioChannels.Channels(numInputs), AudioChannels.Channels(numOutputs),
                    sampleRate,
                    bufferSize);
            }

            _currentSession  = new AudioSession(input, output);
            
            _currentSession.Start(_callbackHandler);
            Console.WriteLine("SUCCESS !");
            Thread.Sleep(500);
            _currentSession.Stop();

            Thread.Sleep(500);
            return _currentSession;
        }

        private XtSample ChooseBestSampleType(AudioSession session, XtSample sample)
        {
            return XtSample.Float32;
        }

        private int ChooseBestSampleRate(IAudioDevice device, int rate)
        {
            Trace.Assert(device != null);
            var rates = device.AvailableSampleRates;
            if (rates == null || rates.Length == 0)
            {
                return 44100;
            }

            if (rate > 0 && rates.Contains(rate))
            {
                return rate;
            }

            rate = device.CurrentSampleRate;
            if (rate > 0 && rates.Contains(rate))
            {
                return rate;
            }

            var lowestAbove44 = 0;
            for (int i = rates.Length; --i >= 0;)
            {
                var sr = rates[i];
                if (sr >= 44100 && (lowestAbove44 == 0 || sr < lowestAbove44))
                {
                    lowestAbove44 = sr;
                }
            }

            if (lowestAbove44 > 0)
            {
                return lowestAbove44;
            }

            return rates[0];
        }

        private double ChooseBestBufferSize(IAudioDevice device)
        {
            Trace.Assert(device != null);
            var (min, max) = device.AvailableBufferSizes;
            return (min + max) / 2;
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
            if (callback != null)
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
        }


        public void AudioDeviceCallback(AudioBuffer input, AudioBuffer output, int numFrames)
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