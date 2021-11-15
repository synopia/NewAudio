using System.Threading;
using NewAudio.Core;

namespace NewAudio.Internal
{
    public class TaskQ
    {
        private IMixBuffer[] _buffers;
        private Barrier _barrier;
        private int _write;
        private EventWaitHandle _readerWait;
        
        public TaskQ(int devices, int bufferCount, AudioFormat format)
        {
            _buffers = new IMixBuffer[bufferCount];
            for (int i = 0; i < bufferCount; i++)
            {
                _buffers[i] = new ByteArrayMixBuffer("Buf " + i, format);
            }
            _readerWait = new AutoResetEvent(false);
            _barrier = new Barrier(devices, barrier =>
            {
                _write = 1 - _write;
                _readerWait.Set();
            });
        }

        public IMixBuffer GetWriteBuffer()
        {
            return _buffers[_write];
        }

        public void Done()
        {
            _barrier.SignalAndWait();
        }

        public IMixBuffer GetReadBuffer()
        {
            _readerWait.WaitOne();
            return _buffers[1 - _write];
        }
    }
}