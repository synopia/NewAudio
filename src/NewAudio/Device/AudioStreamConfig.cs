using System;
using System.Linq;
using NewAudio.Dsp;
using Xt;

namespace NewAudio.Device
{

    public class AudioStreamConfig
    {
        public IAudioDevice? AudioDevice { get; init; }
        public XtService Service => AudioDevice?.Service ?? throw new InvalidOperationException();
        public XtDevice Device => AudioDevice?.Device ?? throw new InvalidOperationException();
        public AudioChannels ActiveOutputChannels { get; set; }
        public AudioChannels ActiveInputChannels { get; set; }
        public int SampleRate { get; set; }
        public XtSample SampleType { get; set; }
        public bool Interleaved { get; set; }
        public double BufferSize { get; set; }
        public bool IsEnabled { get; set; }

        public XtMix Mix => new(SampleRate, SampleType);

        public AudioStreamConfig(IAudioDevice? audioDevice)
        {
            AudioDevice = audioDevice;
        }

        public bool IsValid =>
            AudioDevice.AvailableSampleTypes.Contains(SampleType) &&
            AudioDevice.AvailableSampleRates.Contains(SampleRate) &&
            AudioDevice.AvailableBufferSizes.Item1 <= BufferSize &&
            BufferSize <= AudioDevice.AvailableBufferSizes.Item2 &&
            ActiveInputChannels.Count <= AudioDevice.NumAvailableInputChannels &&
            ActiveOutputChannels.Count <= AudioDevice.NumAvailableOutputChannels;

        
        public void Config(AudioChannels inputChannels, AudioChannels outputChannels, int sampleRate, double bufferSize)
        {
            SampleRate = AudioDevice.ChooseBestSampleRate(sampleRate);
            SampleType = AudioDevice.ChooseBestSampleType(XtSample.Float32);
            BufferSize = AudioDevice.ChooseBestBufferSize(bufferSize);

            ActiveInputChannels = inputChannels.Limit(AudioDevice.NumAvailableInputChannels);
            ActiveOutputChannels = outputChannels.Limit(AudioDevice.NumAvailableOutputChannels);
        }
        
        
        private IAudioStream Factory<TSampleType, TMemoryAccess>(AudioStreamConfig[]? secondary, bool input)
            where TSampleType : struct, ISampleType
            where TMemoryAccess : struct, IMemoryAccess
        {
            if (input)
            {
                return new AudioInputStream<TSampleType, TMemoryAccess>(this);
            }
            
            if (secondary==null || secondary.Length==0)
            {
                if (ActiveInputChannels.Count == 0)
                {
                    return new AudioOutputStream<TSampleType, TMemoryAccess>(this);
                }

                if ((Service.GetCapabilities() & XtServiceCaps.FullDuplex) != 0)
                {
                    return new FullDuplexAudioStream<TSampleType, TMemoryAccess>(this);                    
                }
            } 
            
            if (secondary?.Length == 1 && secondary[0].Device==Device && (Service.GetCapabilities() & XtServiceCaps.FullDuplex) != 0 )
            {
                return new FullDuplexAudioStream<TSampleType, TMemoryAccess>(this);
            }
            
            if( secondary!=null )
            {
                var aggregationAvailable = (Service.GetCapabilities() & XtServiceCaps.Aggregation) != 0 &&
                    secondary.All(s => (s.Service.GetCapabilities() & XtServiceCaps.Aggregation) != 0);

                if (aggregationAvailable)
                {
                    return new AggregateAudioStream<TSampleType, TMemoryAccess>(this, secondary);
                }
                
            }

            throw new InvalidOperationException("No compatible session mode found!");
        }

        public IAudioStream CreateInputStream()
        {
            return CreateStream(Array.Empty<AudioStreamConfig>(), true);
        }
        public IAudioStream CreateStream()
        {
            return CreateStream(Array.Empty<AudioStreamConfig>());
        }
        public IAudioStream CreateStream(AudioStreamConfig inputConfig)
        {
            return CreateStream(new[] { inputConfig });
        }
        public IAudioStream CreateStream(AudioStreamConfig[]? inputConfigs, bool input=false)
        {
            if (Interleaved)
            {
                return SampleType switch
                {
                    XtSample.Float32 => Factory<Float32Sample, Interleaved>(inputConfigs, input),
                    XtSample.Int32 => Factory<Int32LsbSample, Interleaved>(inputConfigs, input),
                    XtSample.Int24 => Factory<Int24LsbSample, Interleaved>(inputConfigs, input),
                    XtSample.Int16 => Factory<Int16LsbSample, Interleaved>(inputConfigs, input),
                    _ => throw new ArgumentOutOfRangeException(nameof(SampleType), SampleType, null)
                };
            }
            else
            {
                return SampleType switch
                {
                    XtSample.Float32 => Factory<Float32Sample, NonInterleaved>(inputConfigs, input),
                    XtSample.Int32 => Factory<Int32LsbSample, NonInterleaved>(inputConfigs, input),
                    XtSample.Int24 => Factory<Int24LsbSample, NonInterleaved>(inputConfigs, input),
                    XtSample.Int16 => Factory<Int16LsbSample, NonInterleaved>(inputConfigs, input),
                    _ => throw new ArgumentOutOfRangeException(nameof(SampleType), SampleType, null)
                };
            }
        }
    }
}