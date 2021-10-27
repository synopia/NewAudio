namespace NewAudio
{
    public class SampleTimer
    {
        private int _time = 0;
        public int Time => _time;

        public int Advance(int samples)
        {
            var t = _time;
            _time += samples;
            return t;
        }
    }
}