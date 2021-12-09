using Xt;

namespace VL.NewAudio.Core
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
    
    public record DeviceName(string Name, XtSystem System, string Id, bool IsInput, bool IsOutput, bool IsDefault)
    {
        public override string ToString()
        {
            var type = System switch
            {
                XtSystem.DirectSound => "DirectSound",
                XtSystem.ASIO => "ASIO",
                XtSystem.WASAPI => "Wasapi",
                _ => ""
            };
            return $"{type}: {Name}";
        }
    }

    public record DeviceCaps
    {
        public DeviceName Name { get; }
        public XtSystem System { get; init; }
        public XtDeviceCaps Caps { get; init; }
            
        public int MaxOutputChannels { get; init; }
        public int MaxInputChannels { get; init; }

        public double BufferSizeMsMin { get; init; }
        public double BufferSizeMsMax { get; init; }

        public bool Interleaved { get; init; }
        public bool NonInterleaved { get; init; }

        public DeviceCaps(DeviceName name)
        {
            Name = name;
        }
    }

}