namespace NewAudio.Core
{
    public enum LifecyclePhase
    {
        Uninitialized,
        Booting,
        Stopped,
        Playing,
        Shutdown,
        Finished
    }
    
    public interface IAudioMessage
    {
        public AudioTime Time { get; set; }
    }

    public struct LifecycleMessage : IAudioMessage
    {
        public AudioTime Time { get; set; }
        public LifecyclePhase Leave { get; private set; }
        public LifecyclePhase Enter { get; private set; }

        public LifecycleMessage(LifecyclePhase leave, LifecyclePhase enter) : this()
        {
            Time = new AudioTime();
            Leave = leave;
            Enter = enter;
        }

        public override string ToString()
        {
            return $"Lifecycle Message: {Time}, {Leave} => {Enter}";
        }
    }

    public interface IAudioDataMessage : IAudioMessage, IAudioFormat
    {
        public bool IsLocked { get; }
        public float[] Data { get; }
    }

    public struct AudioDataRequestMessage : IAudioMessage
    {
        public AudioTime Time { get; set; }
        public int RequestedSamples { get; private set; }

        public float[] Data { get; private set; }

        public AudioDataRequestMessage(int requestedSamples)
        {
            RequestedSamples = requestedSamples;
            Data = null;
            Time = new AudioTime();
        }

        public AudioDataRequestMessage(AudioDataMessage recycle, int requestedSamples) : this(requestedSamples)
        {
            Data = recycle.Data;
        }

        public override string ToString()
        {
            return $"Data Request: {Time}, requested samples={RequestedSamples}, recycling={Data != null}";
        }
    }

    public struct AudioDataMessage : IAudioDataMessage
    {
        public AudioTime Time { get; set; }
        public int SampleCount { get; set; }
        public bool IsLocked { get; private set; }
        public bool IsInterleaved { get; private set; }
        public float[] Data { get; private set; }
        public int Channels { get; private set; }
        public int BufferSize => SampleCount*Channels;

        public AudioDataMessage(IAudioFormat format, int sampleCount):this(null, format, sampleCount)
        {
        }

        public AudioDataMessage(float[] recycled, IAudioFormat format, int sampleCount)
        {
            IsInterleaved = format.IsInterleaved;
            SampleCount = sampleCount;
            Channels = format.Channels;
            Time = new AudioTime();
            IsLocked = false;
            Data = recycled!=null && recycled.Length == sampleCount*Channels ? recycled : new float[sampleCount*Channels];
        }

        public override string ToString()
        {
            return
                $"Data Message: {Time}, SampleCount={SampleCount}, Channels={Channels}, Interleaved={IsInterleaved}, Locked={IsLocked}";
        }
    }
}