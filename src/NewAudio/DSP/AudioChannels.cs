using System;
using System.Linq;
using System.Numerics;
using VL.NewAudio.Core;

namespace VL.NewAudio.Dsp
{
    [Flags]
    public enum AudioChannelType : ulong
    {
        None = 0,
        Mono = 1,
        Left = 1,
        Right = 2,
        Center = 4,
        Unspecified0 = 24
    }

    public readonly struct AudioChannels
    {
        private readonly ulong _channels;

        public int Count => BitOperations.PopCount(_channels);
        public bool IsDisabled => _channels == 0;

        public AudioChannelType ToType()
        {
            return (AudioChannelType)ToLong();
        }

        public AudioChannels Limit(int channels)
        {
            return new AudioChannels((AudioChannelType)(_channels % ((ulong)1 << channels)));
        }

        public ulong ToLong()
        {
            return _channels;
        }

        public ulong Mask => ToLong();

        private AudioChannels(AudioChannelType channels)
        {
            _channels = (ulong)channels;
        }

        public bool this[int channel] => (_channels & ((ulong)1 << channel)) != 0;

        public static AudioChannels FromMask(ulong mask)
        {
            return new AudioChannels((AudioChannelType)mask);
        }

        public static AudioChannels Disabled = new(AudioChannelType.None);
        public static AudioChannels Mono = new(AudioChannelType.Mono);
        public static AudioChannels Stereo = new(AudioChannelType.Left | AudioChannelType.Right);

        public static AudioChannels Channels(int number)
        {
            var value = ((ulong)1 << number) - 1;
            return new AudioChannels((AudioChannelType)value);
        }

        public static AudioChannels UnspecificChannels(int number)
        {
            ulong value = 0;
            for (var i = 0; i < number; i++)
            {
                value += (ulong)(1 << (i + (int)AudioChannelType.Unspecified0));
            }

            return new AudioChannels((AudioChannelType)value);
        }


        public static AudioChannels FromNames(IAudioDevice device, bool input, string[] selectedChannels)
        {
            var channel = 0;
            ulong channelMask = 0;
            var allChannels = input ? device.InputChannelNames : device.OutputChannelNames;
            foreach (var channelName in allChannels)
            {
                if (selectedChannels.Contains(channelName))
                {
                    channelMask |= (ulong)1 << channel;
                }

                channel++;
            }

            return FromMask(channelMask);
        }
    }
}