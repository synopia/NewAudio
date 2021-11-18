using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace NewAudio.Dsp
{
    public class RingBuffer<T>
    {
        private T[] _data;
        private int _allocatedSize;
        private int _writeIndex;
        private int _readIndex;

        public int Size => _allocatedSize-1;
        public int AvailableWrite => GetAvailableWrite(_writeIndex, _readIndex);
        public int AvailableRead => GetAvailableRead(_writeIndex, _readIndex);
        

        public RingBuffer(int size)
        {
            Resize(size);
        }

        public void Resize(int size)
        {
            _allocatedSize = size + 1;
            if (_data!=null)
            {
                ArrayPool<T>.Shared.Return(_data);
              
            } 
            _data = ArrayPool<T>.Shared.Rent(_allocatedSize);
            Clear();
        }

        public void Clear()
        {
            _writeIndex = 0;
            _readIndex = 0;
        }

        public bool Write(T[] array, int count)
        {
            // todo lock
            var writeIndex = _writeIndex;
            var readIndex = _readIndex;

            if (count > GetAvailableWrite(writeIndex, readIndex))
            {
                return false;
            }

            var writeIndexAfter = writeIndex + count;
            if (writeIndex + count > _allocatedSize)
            {
                var countA = _allocatedSize - writeIndex;
                var countB = count - countA;
                Array.Copy(array,0, _data, writeIndex,countA );
                Array.Copy(array,countA, _data, 0,countB );
                writeIndexAfter -= _allocatedSize;
            }
            else
            {
                Array.Copy(array, 0, _data, writeIndex, count);
                if (writeIndexAfter == _allocatedSize)
                {
                    writeIndexAfter = 0;
                }
            }

            _writeIndex = writeIndexAfter;
            return true;
        }

        public bool Read(T[] array, int count)
        {
            // todo lock
            var writeIndex = _writeIndex;
            var readIndex = _readIndex;
            
            if (count > GetAvailableRead(writeIndex, readIndex))
            {
                return false;
            }

            var sourceHandle = GCHandle.Alloc(_data);
            var source = GCHandle.ToIntPtr(sourceHandle);
            var destHandle = GCHandle.Alloc(array);
            var dest = GCHandle.ToIntPtr(destHandle);
            
            int readIndexAfter = readIndex + count;
            if (readIndex + count > _allocatedSize)
            {
                var countA = _allocatedSize - readIndex;
                var countB = count - countA;
                Array.Copy(_data, readIndex, array, 0, countA);
                Array.Copy(_data,  0, array, countA, countB);
                readIndexAfter -= _allocatedSize;
            }
            else
            {
                Array.Copy(_data, readIndex, array, 0, count);
                if (readIndexAfter == _allocatedSize)
                {
                    readIndexAfter = 0;
                }
            }

            _readIndex = readIndexAfter;
            return true;

        }

        private int GetAvailableWrite(int writeIndex, int readIndex)
        {
            var result = readIndex - writeIndex - 1;
            if (writeIndex >= readIndex)
            {
                result += _allocatedSize;
            }

            return result;
        }
        private int GetAvailableRead(int writeIndex, int readIndex)
        {
            if (writeIndex >= readIndex)
            {
                return writeIndex - readIndex;
            }
            return writeIndex+_allocatedSize-readIndex;
        }
    }
}