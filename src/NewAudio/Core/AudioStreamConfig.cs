using System;
using System.Collections.Generic;
using System.Linq;
using VL.NewAudio.Dsp;
using VL.NewAudio.Backend;
using Xt;

namespace VL.NewAudio.Device
{

    public record AudioStreamConfig
    {
        public IAudioDevice AudioDevice { get; }
        public bool SupportsFullDuplex { get; }
        public bool SupportsAggregation { get; }
        public AudioChannels ActiveOutputChannels { get; init; }
        public AudioChannels ActiveInputChannels { get; init; }
        public int SampleRate { get; init; }
        public XtSample SampleType { get; init; }
        public bool Interleaved { get; init; }
        public double BufferSize { get; init; }
        public bool IsEnabled { get; init; }

        public XtMix Mix => new(SampleRate, SampleType);

        public AudioStreamConfig(IAudioDevice audioDevice)
        {
            AudioDevice = audioDevice;
            SupportsFullDuplex = AudioDevice.SupportsFullDuplex;
            SupportsAggregation = AudioDevice.SupportsAggregation;
        }

        public bool IsValid =>
            AudioDevice.AvailableSampleTypes.Contains(SampleType) &&
            AudioDevice.AvailableSampleRates.Contains(SampleRate) &&
            AudioDevice.AvailableBufferSizes.Item1 <= BufferSize &&
            BufferSize <= AudioDevice.AvailableBufferSizes.Item2 &&
            ActiveInputChannels.Count <= AudioDevice.NumAvailableInputChannels &&
            ActiveOutputChannels.Count <= AudioDevice.NumAvailableOutputChannels;


        public AudioStreamConfig Match()
        {
            return this with
            {
                SampleRate = AudioDevice.ChooseBestSampleRate(SampleRate),
                SampleType = AudioDevice.ChooseBestSampleType(XtSample.Float32),
                BufferSize = AudioDevice.ChooseBestBufferSize(BufferSize),

                ActiveInputChannels = ActiveInputChannels.Limit(AudioDevice.NumAvailableInputChannels),
                ActiveOutputChannels = ActiveOutputChannels.Limit(AudioDevice.NumAvailableOutputChannels),
            };
        }

    }
    public class AudioStreamBuilder
    {
        private AudioStreamConfig? _primary;
        public AudioStreamConfig? Primary
        {
            get => _primary;
            set
            {
                _primary = value;
            }
        }

        private AudioStreamConfig[] _secondary = Array.Empty<AudioStreamConfig>();

        public IEnumerable<AudioStreamConfig> Secondary
        {
            get => _secondary;
            set
            {
                _secondary = value.Where(i=>i.ActiveInputChannels.Count>0).ToArray();
            }
        }
        

        private IAudioInputOutputStream Factory<TSampleType, TMemoryAccess>()
            where TSampleType : struct, ISampleType
            where TMemoryAccess : struct, IMemoryAccess
        {
            // if (input)
            // {
                // return new AudioInputStream<TSampleType, TMemoryAccess>(Primary);
            // }

            if (_primary == null)
            {
                throw new InvalidOperationException("No primary output selected!");
            }
            
            if (_secondary.Length==0)
            {
                if (_primary.ActiveInputChannels.Count == 0)
                {
                    return new AudioOutputWithNoInputStream<TSampleType, TMemoryAccess>(_primary);
                }

                if (_primary.SupportsFullDuplex)
                {
                    return new AudioFullDuplexStream<TSampleType, TMemoryAccess>(_primary);                    
                }
            } 
            
            if (_secondary.Length == 1 && _secondary[0].AudioDevice.Id==_primary.AudioDevice.Id && _primary.SupportsFullDuplex )
            {
                return new AudioFullDuplexStream<TSampleType, TMemoryAccess>(_primary);
            }
            
            if( _secondary.Length>0  )
            {
                var aggregationAvailable = _primary.SupportsAggregation &&
                    _secondary.All(s => s.SupportsAggregation);

                if (aggregationAvailable)
                {
                    return new AudioAggregateStream<TSampleType, TMemoryAccess>(_primary, _secondary);
                }
                
            }

            throw new InvalidOperationException("No compatible session mode found!");
        }

        public IAudioInputOutputStream Build()
        {
            if (_primary == null)
            {
                throw new InvalidOperationException("No primary output selected!");
            }

            if (_primary.Interleaved)
            {
                return _primary.SampleType switch
                {
                    XtSample.Float32 => Factory<Float32Sample, Interleaved>(),
                    XtSample.Int32 => Factory<Int32LsbSample, Interleaved>(),
                    XtSample.Int24 => Factory<Int24LsbSample, Interleaved>(),
                    XtSample.Int16 => Factory<Int16LsbSample, Interleaved>(),
                    _ => throw new ArgumentOutOfRangeException(nameof(AudioStreamConfig.SampleType), _primary.SampleType, null)
                };
            }
            return _primary.SampleType switch
            {
                XtSample.Float32 => Factory<Float32Sample, NonInterleaved>(),
                XtSample.Int32 => Factory<Int32LsbSample, NonInterleaved>(),
                XtSample.Int24 => Factory<Int24LsbSample, NonInterleaved>(),
                XtSample.Int16 => Factory<Int16LsbSample, NonInterleaved>(),
                _ => throw new ArgumentOutOfRangeException(nameof(AudioStreamConfig.SampleType), _primary.SampleType, null)
            };
        }
    }
}