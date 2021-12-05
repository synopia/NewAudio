using System;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Dsp;
using Serilog;
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
        void AudioDeviceCallback(AudioBuffer? input, AudioBuffer output, int numFrames);

        void AudioDeviceAboutToStart(IAudioSession session);
        void AudioDeviceStopped();

        void AudioDeviceError(string errorMessage);
    }

    public interface IAudioDevice : IDisposable
    {
        XtService Service { get; }
        DeviceCaps Caps { get; }
        string Name { get; }
        string Id { get; }
        XtSystem System { get; }
        XtDevice Device { get; }

        string[] OutputChannelNames { get; }
        string[] InputChannelNames { get; }
        int[] AvailableSampleRates { get; }
        XtSample[] AvailableSampleTypes { get; }
        (double, double) AvailableBufferSizes { get; }
        double DefaultBufferSize { get; }

        int NumAvailableInputChannels { get; }
        int NumAvailableOutputChannels { get; }
        
        bool HasControlPanel { get; }

        bool ShowControlPanel();

        XtSample ChooseBestSampleType(XtSample sample);
        int ChooseBestSampleRate(int rate);
        double ChooseBestBufferSize(double bufferSize);
        
    }

    public class XtAudioDevice : IAudioDevice
    {
        public XtService Service { get; init; }
        public DeviceCaps Caps { get; }
        public string Name => Caps.Name;
        public string Id => Caps.DeviceId;
        public XtSystem System => Caps.System;
        public XtDevice Device { get; init; }
        
        private readonly HashSet<int> _availableSampleRates = new();
        private readonly HashSet<XtSample> _availableSampleTypes = new();
        private (double, double) _availableBufferSizes;
        private (double, double) _preferredBufferSizes;

        private bool _disposed;
        private ILogger _logger = Resources.GetLogger<IAudioDevice>();

        public XtAudioDevice(XtService xtService, DeviceCaps caps)
        {
            Service = xtService;
            Caps = caps;
            _logger.Information("Opening device '{Name}' ({Id})", Caps.Name, Caps.DeviceId);
            Device = xtService.OpenDevice(Id);
            
            UpdateSampleRates();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.Information("Disposing device {Name}", Caps.Name);
                _disposed = true;
                _availableSampleRates.Clear();
                _availableSampleTypes.Clear();
                Device.Dispose();
            }
        }

        private string[] GetChannelNames(bool output)
        {
            var cnt = Device.GetChannelCount(output);
            var result = new string[cnt];
            for (int i = 0; i < cnt; i++)
            {
                result[i] = Device.GetChannelName(output, i);
            }

            return result;
        }

        private void UpdateSampleRates()
        {
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
                    if (Device.SupportsFormat(format))
                    {
                        _availableSampleRates.Add(rate);
                        _availableSampleTypes.Add((XtSample)sample);
                        var bufferSize = Device.GetBufferSize(format);
                        minBufferSize = Math.Min(bufferSize.min, minBufferSize);
                        maxBufferSize = Math.Max(bufferSize.max, maxBufferSize);
                        minPreferredBufferSize = Math.Min(bufferSize.current, minPreferredBufferSize);
                        maxPreferredBufferSize = Math.Max(bufferSize.current, maxPreferredBufferSize);
                    }
                }
            }

            _availableBufferSizes = (minBufferSize, maxBufferSize);
            _preferredBufferSizes = (minPreferredBufferSize, maxPreferredBufferSize);
            
            _logger.Information("Available sample rates: {SampleRates}", _availableSampleRates);
            _logger.Information("Available buffer size: {MinBufferSize} -> {MaxBufferSize}", minBufferSize, maxBufferSize);
            _logger.Information("Available sample types: {SampleType}", _availableSampleTypes);
        }

        
        public int NumAvailableInputChannels => Caps.MaxInputChannels;

        public int NumAvailableOutputChannels => Caps.MaxOutputChannels;

        public string[] OutputChannelNames => GetChannelNames(true);
        public string[] InputChannelNames => GetChannelNames(false);

        public int[] AvailableSampleRates => _availableSampleRates.ToArray();
        public (double, double) AvailableBufferSizes => _availableBufferSizes;
        public XtSample[] AvailableSampleTypes => _availableSampleTypes.ToArray();
        public double DefaultBufferSize => _preferredBufferSizes.Item1;


        public XtSample ChooseBestSampleType(XtSample sample)
        {
            if (AvailableSampleTypes.Contains(sample))
            {
                return sample;
            }

            if (AvailableSampleTypes.Contains(XtSample.Float32))
            {
                return XtSample.Float32;
            }
            if (AvailableSampleTypes.Contains(XtSample.Int32))
            {
                return XtSample.Int32;
            }
            if (AvailableSampleTypes.Contains(XtSample.Int24))
            {
                return XtSample.Int24;
            }
            if (AvailableSampleTypes.Contains(XtSample.Int16))
            {
                return XtSample.Int16;
            }

            return XtSample.Float32;
        }

        public int ChooseBestSampleRate(int rate)
        {
            
            if (AvailableSampleRates.Length == 0)
            {
                return 44100;
            }

            if (rate > 0 && AvailableSampleRates.Contains(rate))
            {
                return rate;
            }

            var lowestAbove44 = 0;
            for (int i = AvailableSampleRates.Length; --i >= 0;)
            {
                var sr = AvailableSampleRates[i];
                if (sr >= 44100 && (lowestAbove44 == 0 || sr < lowestAbove44))
                {
                    lowestAbove44 = sr;
                }
            }

            if (lowestAbove44 > 0)
            {
                return lowestAbove44;
            }

            return AvailableSampleRates[0];
        }

        public double ChooseBestBufferSize(double bufferSize)
        {
            var (min, max) = AvailableBufferSizes;
            if (min <= bufferSize && bufferSize <= max)
            {
                return bufferSize;
            }
            return (min + max) / 2;
        }


        public bool ShowControlPanel()
        {
            return false;
        }

        public bool HasControlPanel => false;
    }
}