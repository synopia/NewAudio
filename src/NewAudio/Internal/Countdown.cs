using System.Threading;

namespace NewAudio.Internal
{
    public class Countdown
    {
        private object _locker = new();
        private int _value;

        public Countdown(int value)
        {
            _value = value;
        }
        
        public void Signal() {}

        public void AddCount(int amount)
        {
            lock (_locker)
            {
                _value += amount;
                if (_value <= 0)
                {
                    Monitor.PulseAll(_locker);
                }
            }
        }

        public void Wait()
        {
            lock (_locker)
            {
                while (_value>0)
                {
                    Monitor.Wait(_locker);
                }
            }
        }
    }
}