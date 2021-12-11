namespace VL.NewAudio.Core
{
    public interface IPositionalAudioSource : IAudioSource
    {
        ulong NextReadPos { get; set; }
        ulong TotalLength { get; }
        bool IsLooping { get; }
    }
}