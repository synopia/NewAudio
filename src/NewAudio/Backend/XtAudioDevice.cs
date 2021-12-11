using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using VL.NewAudio.Core;
using Xt;

namespace VL.NewAudio.Backend
{
    public class XtAudioDevice : IAudioDevice
    {
        private readonly ILogger _logger = Resources.GetLogger<XtAudioDevice>();
        public readonly XtDevice XtDevice;
        public readonly XtService XtService;
        public DeviceCaps Caps { get; }
        public string Name => Caps.Name.Name;
        public string Id => Caps.Name.Id;
        public XtSystem System => Caps.System;

        private readonly HashSet<int> _availableSampleRates = new();
        private readonly HashSet<XtSample> _availableSampleTypes = new();
        private (double, double) _availableBufferSizes;
        private (double, double) _preferredBufferSizes;

        public bool SupportsFullDuplex => (XtService.GetCapabilities() & XtServiceCaps.FullDuplex) != 0;
        public bool SupportsAggregation => (XtService.GetCapabilities() & XtServiceCaps.Aggregation) != 0;

        private bool _disposed;

        public XtAudioDevice(XtService xtService, DeviceCaps caps)
        {
            XtService = xtService;
            Caps = caps;
            _logger.Information("Opening device '{Name}' ({Id})", Caps.Name, Caps.Name.Id);
            XtDevice = xtService.OpenDevice(Id);

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
                XtDevice.Dispose();
            }
        }

        public bool SupportsFormat(XtFormat format)
        {
            return XtDevice.SupportsFormat(format);
        }

        private string[] GetChannelNames(bool output)
        {
            var cnt = XtDevice.GetChannelCount(output);
            var result = new string[cnt];
            for (var i = 0; i < cnt; i++)
            {
                result[i] = XtDevice.GetChannelName(output, i);
            }

            return result;
        }

        private void UpdateSampleRates()
        {
            _availableSampleRates.Clear();
            _availableSampleTypes.Clear();

            var minBufferSize = double.MaxValue;
            var maxBufferSize = 0.0;
            var minPreferredBufferSize = double.MaxValue;
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
                        new XtChannels(Math.Min(2, Caps.MaxInputChannels), 0, Math.Min(2, Caps.MaxOutputChannels),
                            0));
                    if (XtDevice.SupportsFormat(format))
                    {
                        _availableSampleRates.Add(rate);
                        _availableSampleTypes.Add((XtSample)sample);
                        var bufferSize = XtDevice.GetBufferSize(format);
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
            _logger.Information("Available buffer size: {MinBufferSize} -> {MaxBufferSize}", minBufferSize,
                maxBufferSize);
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
            for (var i = AvailableSampleRates.Length; --i >= 0;)
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