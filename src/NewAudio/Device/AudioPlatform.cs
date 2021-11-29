using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewAudio.Devices;
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
    }

    public class CallbackHandler : IAudioDeviceCallback
    {
        private XtAudioPlatform _owner;

        public CallbackHandler(XtAudioPlatform owner)
        {
            _owner = owner;
        }

        public void AudioDeviceCallback(AudioBuffer input, AudioBuffer output, int numFrames)
        {
            _owner.AudioDeviceCallback(input, output, numFrames);
        }

        public void AudioDeviceAboutToStart(IAudioDevice device)
        {
            _owner.AudioDeviceAboutToStart(device);
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
        private IAudioDevice _currentDevice;

        public XtAudioPlatform(XtPlatform platform)
        {
            _platform = platform;
            _callbackHandler = new CallbackHandler(this);
            _tempBuffer = new AudioBuffer();

            _service = new XtAudioService(platform);
        }

        public void Dispose()
        {
            _currentDevice?.Dispose();
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
            if (_currentDevice != null)
            {
                _currentDevice.Stop();
            }
        }

        public void Open(string inputDeviceId,string outputDeviceId, int numInputs, int numOutputs)
        {
            Close();

            ScanIfNeeded();

            _currentDevice = _service.CreateDevice(outputDeviceId, inputDeviceId);

            numInputs = Math.Min(numInputs, _currentDevice.InputChannelNames.Length);
            numOutputs = Math.Min(numOutputs, _currentDevice.OutputChannelNames.Length);

            // var sampleType = ChooseBestSampleType(_currentDevice, XtSample.Float32);
            var sampleRate = ChooseBestSampleRate(_currentDevice, 0);
            var bufferSize = ChooseBestBufferSize(_currentDevice);

            var res = _currentDevice.Open(AudioChannels.Channels(numInputs), AudioChannels.Channels(numOutputs),
                sampleRate,
                bufferSize);

            if (res)
            {
                _currentDevice.Start(_callbackHandler);
                Console.WriteLine("SUCCESS !");
                Thread.Sleep(500);
                _currentDevice.Stop();
            }

            Thread.Sleep(500);
        }

        private XtSample ChooseBestSampleType(IAudioDevice device, XtSample sample)
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

            if (_currentDevice != null)
            {
                callback?.AudioDeviceAboutToStart(_currentDevice);
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
                bool needReinit = _currentDevice != null;
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

        public void AudioDeviceAboutToStart(IAudioDevice device)
        {
            lock (AudioProcessLock)
            {
                for (int i = _callbacks.Count; --i >= 0;)
                {
                    _callbacks[i].AudioDeviceAboutToStart(device);
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