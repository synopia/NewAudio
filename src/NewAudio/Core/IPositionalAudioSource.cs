namespace VL.NewAudio.Core
{
    public interface IPositionalAudioSource : IAudioSource
    {
        long NextReadPos { get; set; }
        long TotalLength { get; }
        bool IsLooping { get; }
    }
}