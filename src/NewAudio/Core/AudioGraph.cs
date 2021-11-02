using System;

namespace NewAudio.Core
{
    public class AudioGraph : IDisposable
    {
        private int _id;

        public void Dispose()
        {
            _id = 0;
        }

        public int GetBufferId()
        {
            return _id++;
        }


        public void Add(AudioLink link)
        {
        }

        public void Remove(AudioLink link)
        {
        }

        public string DebugInfo()
        {
            return $"Buffers: {_id}";
        }
    }
}