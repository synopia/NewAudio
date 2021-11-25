using System;
using NewAudio.Core;
using NewAudio.Dsp;
using Xt;

namespace NewAudio.Devices
{
    public struct ChannelConfig
    {
        public int OutputChannels;
        public int InputChannels;
        public int OutputOffset;
        public int InputOffset;
    }

    public struct FormatConfig
    {
        public int SampleRate;
        public double BufferSizeMs;
        public XtSample SampleType;
        public int FramesPerBlock;
    }

    public delegate void BeforeDeviceConfigChange(DeviceState device);
    public delegate void AfterDeviceConfigChange(DeviceState device);

    public delegate AudioBuffer OnAudioBufferRequest(int frames);
    public delegate void BeforeAudioBufferFill(int frames);
    public delegate void AfterAudioBufferFill(int frames);

    public class GraphState
    {
        public string GraphId;
        public BeforeAudioBufferFill BeforeAudioBufferFill;
        public AfterAudioBufferFill AfterAudioBufferFill;
        public BeforeDeviceConfigChange BeforeDeviceConfigChange;
        public AfterDeviceConfigChange AfterDeviceConfigChange;
    }
    public class Session
    {
        public string SessionId;
        public string DeviceId;
        public string GraphId;
        public ChannelConfig ChannelConfig;

        public ChannelConfig Channels;
        public FormatConfig Format;
        
        public bool IsInitialized;
        public bool IsProcessing;

        public OnAudioBufferRequest OnAudioBufferRequest;
    }
    
    public class DeviceState
    {
        public string DeviceId;
        public DeviceCaps Caps;
        public ChannelConfig Channels;
        public XtEnumFlags CurrentState;

        public FormatConfig Format;

        public bool IsInitialized;
        public bool IsProcessing;
        public AudioBuffer OutputBuffer;
        public IConvertReader ConvertReader;
        public IConvertWriter ConvertWriter;
        
        public IXtDevice XtDevice;
        public IXtStream XtStream;
        public bool ThrowsException;
        public string Error;
    }
    
    public class DeviceCaps
    {
        public string DeviceId;
        public string Name;
        public XtSystem System;
        public XtDeviceCaps Caps;
        public XtEnumFlags InOut;
            
        public int MaxOutputChannels;
        public int MaxInputChannels;

        public double BufferSizeMsMin;
        public double BufferSizeMsMax;

        public bool Interleaved;
        public bool NonInterleaved;
    }
}