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

        IAudioSession Open(IAudioDevice? inputDevice, IAudioDevice outputDevice, AudioChannels inputChannels,
            AudioChannels outputChannels, int sampleRate, double bufferSize);

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

    public class XtAudioControl : IAudioControl, IAudioDeviceCallback
    {
        private CallbackHandler _callbackHandler;
        public object AudioProcessLock { get; } = new();
        private List<IAudioDeviceCallback> _callbacks = new();
        private AudioBuffer _tempBuffer;

        private IAudioService _service;
        private bool _scannedForDevices = false;
        private AudioSession? _currentSession;

        public bool IsRunning => _currentSession?.IsRunning ?? false;

        public XtAudioControl(IAudioService service)
        {
            _callbackHandler = new CallbackHandler(this);
            _tempBuffer = new AudioBuffer();

            _service = service;
        }

        public void Dispose()
        {
            _currentSession?.Dispose();
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

        public IAudioSession Open(IAudioDevice? inputDevice, IAudioDevice outputDevice, AudioChannels inputChannels, AudioChannels outputChannels, int sampleRate, double bufferSize)
        {
            Close();

            ScanIfNeeded();

            // numInputs = Math.Min(numInputs, inputDevice.InputChannelNames.Length);
            // numOutputs = Math.Min(numOutputs, outputDevice.OutputChannelNames.Length);

            // var sampleType = ChooseBestSampleType(_currentDevice, XtSample.Float32);
            sampleRate = ChooseBestSampleRate(outputDevice, sampleRate);
            bufferSize = ChooseBestBufferSize(outputDevice, bufferSize);

            // todo input

            if (outputChannels.Count > 0)
            {
                if (inputDevice != null && outputDevice.Id != inputDevice.Id)
                {
                    outputDevice.Open(AudioChannels.Disabled, outputChannels,
                        sampleRate,
                        bufferSize);
                    inputDevice.Open(inputChannels, AudioChannels.Disabled,
                        sampleRate,
                        bufferSize);
                }
                else
                {
                    outputDevice.Open(inputChannels, outputChannels, sampleRate, bufferSize);
                }

                _currentSession = new AudioSession(inputDevice, outputDevice);

                _currentSession.Start(_callbackHandler);

                return _currentSession;
            }

            return null;
        }

        private XtSample ChooseBestSampleType(AudioSession session, XtSample sample)
        {
            return XtSample.Float32;
        }

        private int ChooseBestSampleRate(IAudioDevice device, int rate)
        {
            Trace.Assert(device != null);
            var rates = device!.AvailableSampleRates;
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

        private double ChooseBestBufferSize(IAudioDevice device, double bufferSize)
        {
            Trace.Assert(device != null);
            var (min, max) = device.AvailableBufferSizes;
            if (min <= bufferSize && bufferSize <= max)
            {
                return bufferSize;
            }
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