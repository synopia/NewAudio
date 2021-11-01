using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NewAudio.Internal
{
    public class LockFreeCircularBuffer
    {
        private readonly float[] _trait;
        private int _head;
        private int _tail;
        
        public int Capacity { get; }

        public int Count
        {
            get
            {
                var tailSnapshot = _tail;
                return _head - tailSnapshot;
            }
        }

        public int AddedCount => _head;
        public int RemovedCount => _tail;

        public LockFreeCircularBuffer(int capacity)
        {
            Capacity = capacity;
            _trait = new float[capacity];
        }

        public int Write(float[] data, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var res = Add(data[offset + i]);
                if (!res)
                {
                    return i-1;
                }
            }

            return count;
        }

        public int Read(float[] data, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                data[i+offset] = Read();
            }

            return count;
        }

        public bool Add(float value)
        {
            while (true)
            {
                var tailSnapshot = _tail;
                var headSnapshot = _head;
                if (headSnapshot - tailSnapshot >= Capacity)
                {
                    return false;
                }

                var head = Interlocked.CompareExchange(ref _head, headSnapshot + 1, headSnapshot);
                if (head != headSnapshot)
                {
                    continue;
                }

                var index = head % Capacity;
                _trait[index] = value;
                return true;
            }
        }
        public bool TryAdd(float value, int maxSpins=0)
        {
            if (maxSpins <= 0)
            {
                return Add(value);
            }
            var spins = maxSpins;
            while (true)
            {
                var tailSnapshot = _tail;
                var headSnapshot = _head;
                if (headSnapshot - tailSnapshot >= Capacity)
                {
                    return false;
                }

                var head = Interlocked.CompareExchange(ref _head, headSnapshot + 1, headSnapshot);
                if (head != headSnapshot)
                {
                    if (spins-- == 0)
                    {
                        return false;
                    }
                    continue;
                }

                var index = head % Capacity;
                _trait[index] = value;
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Read()
        {
            var index = _tail % Capacity;
            while (true)
            {
                var value = _trait[index];
                if (Single.IsNaN(value))
                {
                    continue;
                }
                _trait[index] = Single.NaN;
                _tail++;
                return value;
            }
        }
    }
}