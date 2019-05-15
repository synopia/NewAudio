namespace VL.NewAudio
{
    public class Silence
    {
        private readonly AudioSampleBuffer output = AudioSampleBuffer.Silence();

        public AudioSampleBuffer Update()
        {
            return output;
        }
    }
}