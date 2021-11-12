﻿using System.Buffers;

namespace NewAudio.Core
{
    public struct AudioDataRequestMessage
    {
        public AudioTime Time { get; set; }
        public int RequestedSamples { get; }

        public float[] ReusableDate { get; }

        public AudioDataRequestMessage(int requestedSamples)
        {
            RequestedSamples = requestedSamples;
            ReusableDate = null;
            Time = new AudioTime();
        }

        public AudioDataRequestMessage(float[] reusableDate, int requestedSamples) : this(requestedSamples)
        {
            ReusableDate = reusableDate;
        }

        public override string ToString()
        {
            return $"Data Request: {Time}, requested samples={RequestedSamples}, recycling={ReusableDate != null}";
        }
    }

    public struct AudioDataMessage
    {
        public AudioTime Time { get; set; }
        public int SampleCount { get; set; }
        public bool IsLocked { get; }
        public bool IsInterleaved { get; }
        public float[] Data { get; }
        public int Channels { get; }
        public int BufferSize => SampleCount * Channels;

        public AudioDataMessage(AudioFormat format, int sampleCount) : this(null, format, sampleCount)
        {
        }

        public AudioDataMessage(float[] data, AudioFormat format, int sampleCount)
        {
            IsInterleaved = format.IsInterleaved;
            SampleCount = sampleCount;
            Channels = format.Channels;
            Time = new AudioTime(0, 0);
            IsLocked = false;
            Data = data != null && data.Length == sampleCount * Channels
                ? data
                : ArrayPool<float>.Shared.Rent(sampleCount * Channels);
        }

        public override string ToString()
        {
            return
                $"Data Message: {Time}, SampleCount={SampleCount}, Channels={Channels}, Interleaved={IsInterleaved}, Locked={IsLocked}";
        }
    }
}