using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewAudio.Dsp;
using Xt;

namespace NewAudio.Device
{
    public enum SamplingFrequency
    {
        Hz8000 = 8000,
        Hz11025 = 11025,
        Hz16000 = 16000,
        Hz22050 = 22050,
        Hz32000 = 32000,
        Hz44056 = 44056,
        Hz44100 = 44100,
        Hz48000 = 48000,
        Hz88200 = 88200,
        Hz96000 = 96000,
        Hz176400 = 176400,
        Hz192000 = 192000,
        Hz352800 = 352800
    }

    public interface IAudioDeviceCallback
    {
        // void AudioDeviceCallback(Memory<float>[] inputChannelData, int numInputChannels,
            // Memory<float>[] outputChannelData, int numOutputChannels, int numFrames);
        void AudioDeviceCallback(AudioBuffer input, AudioBuffer output, int numFrames);

        void AudioDeviceAboutToStart(IAudioSession session);
        void AudioDeviceStopped();

        void AudioDeviceError(string errorMessage);
    }

    public interface IAudioDevice : IDisposable
    {
        string Name { get; }
        string Id { get; }
        XtSystem System { get; }

        string[] OutputChannelNames { get; }
        string[] InputChannelNames { get; }
        int[] AvailableSampleRates { get; }
        XtSample[] AvailableSampleType { get; }
        (double, double) AvailableBufferSizes { get; }
        double DefaultBufferSize { get; }

        bool Open(AudioChannels inputChannels, AudioChannels outputChannels, int sampleRate, double bufferSize);

        // void Close();

        bool IsOpen();
        // void Start(IAudioDeviceCallback callback);
        // void Stop();
        // bool IsPlaying();
        string GetLastError();

        // int CurrentFramesPerBlock { get; }
        int CurrentSampleRate { get; }
        double CurrentBufferSize { get; }
        XtSample CurrentSampleType { get; }
        AudioChannels ActiveOutputChannels { get; }
        AudioChannels ActiveInputChannels { get; }

        int AvailableInputChannels { get; }
        int AvailableOutputChannels { get; }
        
        // double OutputLatency { get; }
        // double InputLatency { get; }
        bool HasControlPanel { get; }

        bool ShowControlPanel();

        // bool SetAudioPreprocessingEnabled(bool b);
        // int XRunCount { get; }

        AudioStreamConfig GetConfig(bool input);
    }


    public class XtAudioDevice : IAudioDevice
    {
        private readonly XtService _service;
        public readonly DeviceCaps Caps;
        public string Name => Caps.Name;
        public string Id => Caps.DeviceId;
        public XtSystem System => Caps.System;

        private XtDevice? _device;
        public XtDevice? Device => _device;
        
        private string? _error;

        private HashSet<int> _availableSampleRates = new();
        private HashSet<XtSample> _availableSampleTypes = new();
        private (double, double) _availableBufferSizes;
        private (double, double) _preferredBufferSizes;

        private int _currentSampleRate;
        private double _currentBufferSize;
        private XtSample _currentSampleType;


        private AudioChannels _currentChannelsOut;
        private AudioChannels _currentChannelsIn;

        private bool _deviceIsOpen;
        
        public XtAudioDevice(XtService xtService, DeviceCaps caps)
        {
            _service = xtService;
            Caps = caps;

            try
            {
                _device = xtService.OpenDevice(Id);
                UpdateSampleRates();
            }
            catch (XtException e)
            {
                _device = null;
                _error = e.Message;
            }
        }

        public AudioStreamConfig GetConfig(bool input)
        {
            return new AudioStreamConfig()
            {
                Device = _device,
                ActiveChannels = input ? ActiveInputChannels : ActiveOutputChannels,
                SampleRate = CurrentSampleRate,
                SampleType = CurrentSampleType,
                BufferSize = CurrentBufferSize,
                Interleaved = Caps.Interleaved
            };
        }

        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // _session?.Stop();
                // _session?.Dispose();
                _device?.Dispose();

                // _session = null;
                // _device = null;
            }
        }

        private string[] GetChannelNames(bool output)
        {
            var cnt = _device.GetChannelCount(output);
            var result = new string[cnt];
            for (int i = 0; i < cnt; i++)
            {
                result[i] = _device.GetChannelName(output, i);
            }

            return result;
        }

        private void UpdateSampleRates()
        {
            Trace.Assert(_device != null);
            _availableSampleRates.Clear();
            _availableSampleTypes.Clear();

            var minBufferSize = Double.MaxValue;
            var maxBufferSize = 0.0;
            var minPreferredBufferSize = Double.MaxValue;
            var maxPreferredBufferSize = 0.0;
            foreach (var value in Enum.GetValues(typeof(SamplingFrequency)))
            {
                var rate = (int)value;
                foreach (var sample in Enum.GetValues(typeof(XtSample)))
                {
                    if ((XtSample)sample == XtSample.UInt8)
                    {
                        continue;
                    }
                    var format = new XtFormat(new XtMix(rate, (XtSample)sample),
                        new XtChannels(Math.Min(2,Caps.MaxInputChannels), 0, Math.Min(2,Caps.MaxOutputChannels),
                            0));
                    // Console.WriteLine($"Testing {rate}, {sample}, {format.channels.inputs}, {format.channels.outputs}");
                    if (_device.SupportsFormat(format))
                    {
                        // Console.WriteLine($"Format supported: {sample}, {rate}");
                        _availableSampleRates.Add(rate);
                        _availableSampleTypes.Add((XtSample)sample);
                        var bufferSize = _device.GetBufferSize(format);
                        minBufferSize = Math.Min(bufferSize.min, minBufferSize);
                        maxBufferSize = Math.Max(bufferSize.max, maxBufferSize);
                        minPreferredBufferSize = Math.Min(bufferSize.current, minPreferredBufferSize);
                        maxPreferredBufferSize = Math.Max(bufferSize.current, maxPreferredBufferSize);
                    }
                }
            }

            _availableBufferSizes = (minBufferSize, maxBufferSize);
            _preferredBufferSizes = (minPreferredBufferSize, maxPreferredBufferSize);
        }
        public int AvailableInputChannels => Caps.MaxInputChannels;

        public int AvailableOutputChannels => Caps.MaxOutputChannels;

        public string[] OutputChannelNames => GetChannelNames(true);
        public string[] InputChannelNames => GetChannelNames(false);

        public int[] AvailableSampleRates => _availableSampleRates.ToArray();
        public (double, double) AvailableBufferSizes => _availableBufferSizes;
        public XtSample[] AvailableSampleType => _availableSampleTypes.ToArray();
        public double DefaultBufferSize => _preferredBufferSizes.Item1;
        // public int XRunCount => _session.XRuns;
        // public int CurrentFramesPerBlock => _session.FramesPerBlock;
        public int CurrentSampleRate => _currentSampleRate;
        public double CurrentBufferSize => _currentBufferSize;
        public XtSample CurrentSampleType => _currentSampleType;
        public AudioChannels ActiveOutputChannels => _currentChannelsOut;
        public AudioChannels ActiveInputChannels => _currentChannelsIn;

        // public double OutputLatency => _session.OutputLatency;
        // public double InputLatency => _session.InputLatency;

        // private AudioSession _session;

        public bool Open(AudioChannels inputChannels, AudioChannels outputChannels, int sampleRate, double bufferSize)
        {
            // Close();

            if (_availableSampleTypes.Count == 0)
            {
                return false;
            }
            // Trace.Assert(_currentCallback == null);
            Trace.Assert(_device != null);
            // Trace.Assert(_session == null);
            Trace.Assert(_availableSampleTypes.Count > 0);

            _currentSampleRate = sampleRate;

            _currentSampleType = _availableSampleTypes.First();
            _currentBufferSize = bufferSize > 0 ? bufferSize : DefaultBufferSize;

            var totalNumInputChannels = _device.GetChannelCount(false);
            var totalNumOutputChannels = _device.GetChannelCount(true);

            _currentChannelsIn = inputChannels.Limit(totalNumInputChannels);
            _currentChannelsOut = outputChannels.Limit(totalNumOutputChannels);

            _deviceIsOpen = true;
            return _deviceIsOpen;
        }


        /*
        public void Close()
        {
            Stop();
        }

        public void Stop()
        {
            Trace.Assert(_device != null);

            _session?.Stop();
            _running = false;
            SetCallback(null);
        }
        */

        public bool IsOpen()
        {
            return _deviceIsOpen;
        }

        /*
        public bool IsPlaying()
        {
            return _currentCallback != null;
        }
        */


        /*
        public void Start(IAudioDeviceCallback callback)
        {
            Trace.Assert(_deviceIsOpen);
            Trace.Assert(_device!=null);
            
            if (callback != _currentCallback)
            {
                callback?.AudioDeviceAboutToStart(this);
                
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
                    _session.Start();
                }
                _currentCallback = callback;
            }
        }
        */

        /*
        private void SetCallback(IAudioDeviceCallback callback)
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
        */
        
        public string GetLastError()
        {
            return _error;
        }

        public bool ShowControlPanel()
        {
            return false;
        }

        public bool HasControlPanel => false;
    }
}