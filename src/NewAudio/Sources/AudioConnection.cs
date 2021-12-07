using VL.NewAudio.Sources;

namespace VL.NewAudio.Device
{
    public class AudioConnection
    {
        public IAudioSource Source { get; }

        public AudioConnection(IAudioSource source)
        {
            Source = source;
        }
    }
}