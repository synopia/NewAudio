namespace NewAudio
{
    public class SampleTimer
    {
        private int time = 0;

        public int Advance(int samples)
        {
            return time++;
        }
    }
}