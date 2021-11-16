using System;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using NewAudio.Core;
using VL.Lib.Adaptive;

namespace NewAudio.Internal
{
    public struct DataSlot
    {
        public int Offset;
        public int Channels;
        public MSQueue<float[]> Q;
        public bool Interleaved;
    }

    public interface IMixBuffer
    {
        void WriteChannel(int channel, float[] data);
        void WriteChannelsInterleaved(int offset, int channels, float[] data);
        float[] GetFloatArray();
        byte[] Data { get; }
    }

    public class MixBuffers
    {
        private IMixBuffer[] _buffers;

        // private Barrier _barrier;
        private int _write;
        private int _read;
        private int _count;
        private CountdownEvent _countdown;
        private DataSlot[] _slots;
        private AutoResetEvent _writeEvent;
        private AutoResetEvent[] _writeEvents;
        private AudioFormat _format;

        public MixBuffers(DataSlot[] slots, AudioFormat format)
        {
            _format = format;
            var devices = slots.Length;
            _slots = slots;

            /*
            _writeEvent = EventManager.GenerateAutoResetEvent(); //new AutoResetEvent(false);
            _writeEvents = new AutoResetEvent[devices];
            for (int s = 0; s < devices; s++)
            {
                _writeEvents[s] = EventManager.GenerateChildEvent(_writeEvent);
            }
        */
        }

        public MixBuffers(int devices, int bufferCount, AudioFormat format)
        {
            _format = format;
            _buffers = new IMixBuffer[bufferCount];
            for (int i = 0; i < bufferCount; i++)
            {
                _buffers[i] = new ByteArrayMixBuffer("Buf " + i, format);
            }

            _count = devices;
            _countdown = new CountdownEvent(devices);


            /*
            _barrier = new Barrier(devices+1, barrier =>
            {
                _read = _write;
                _write = 1 - _write;
                Array.Clear(_buffers[_write].Data, 0, _buffers[_write].Data.Length);
            });
        */
        }

        public void DecreaseDevices()
        {
            _count--;
        }

        public void IncreaseDevices()
        {
            _count++;
        }


        public void SetData(int slot, float[] data)
        {
            // if (_slots[_write][slot].Data == null)
            // {
            // _writeEvents[slot].WaitOne();
            _slots[slot].Q.enqueue(data);
            // }
            // else
            // {
            // _slots[_write][slot].Data = data;
            // }
        }

        public unsafe int FillBuffer(float[] buffer)
        {
            int length = _format.BufferSize;
            Array.Clear(buffer, 0, buffer.Length);
            var res = false;
            fixed (float* ptr = buffer)
            {
                var intPtr = new IntPtr(ptr);

                res = WriteChannelsInterleaved(intPtr);
            }

            return res ? length : 0;
        }

        public unsafe int FillBuffer(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Array.Clear(buffer, 0, buffer.Length);
            fixed (byte* ptr = buffer)
            {
                var intPtr = new IntPtr(ptr);

                WriteChannelsInterleaved(intPtr);
            }

            return count;
            /*
            _read = _write;
            _write = 1 - _write;
            for (int i = 0; i < _slots[_write].Length; i++)
            {
                _slots[_write][i].Data = null;
            }

            EventManager.SetAll(_writeEvent);
            return count;
        */
        }

        public IMixBuffer GetWriteBuffer(CancellationToken cancellationToken)
        {
            return _buffers[_write];
            // _barrier.SignalAndWait(cancellationToken);
            // return !cancellationToken.IsCancellationRequested ? _buffers[_write] : null;
        }

        public int ReadPlayBuffer(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var buf = GetReadBuffer(cancellationToken);
            if (buf != null)
            {
                Array.Copy(buf.Data, 0, buffer, offset, count);
                return count - offset;
            }

            return 0;
        }

        public IMixBuffer GetReadBuffer(CancellationToken cancellationToken)
        {
            _countdown.Wait(cancellationToken);
            _read = _write;
            _write = 1 - _write;
            Array.Clear(_buffers[_write].Data, 0, _buffers[_write].Data.Length);
            _countdown.Reset(_count);
            // _barrier.SignalAndWait(cancellationToken);
            // var r = _readerWait.WaitOne(1);

            return !cancellationToken.IsCancellationRequested ? _buffers[_read] : null;
        }

        public static unsafe float[] CopyByteToFloat(byte[] buf)
        {
            var result = new float[buf.Length / 4];
            Buffer.BlockCopy(buf, 0, result, 0, buf.Length);
            /*
            fixed (float* ptr = result)
            {
                var intPtr = new IntPtr(ptr);
                Marshal.Copy(buf, 0, intPtr, buf.Length);                
            }
            */

            return result;
        }

        public static unsafe byte[] CopyFloatToByte(float[] buf)
        {
            var result = new byte[buf.Length * 4];
            fixed (byte* ptr = result)
            {
                var intPtr = new IntPtr(ptr);
                Marshal.Copy(buf, 0, intPtr, buf.Length);
            }

            return result;
        }

        public unsafe bool WriteChannelsInterleaved(IntPtr intPtr) // byte[] target
        {
            // Array.Clear(target, 0, target.Length);
            float[][] dequedData = new float[_slots.Length][];
            bool[] success = new bool[_slots.Length];
            bool res = true;
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                success[i] = slot.Q.deque(ref dequedData[i]);
                if (!success[i]) res = false;
            }

            if (!res)
            {
                return false;
            }

            // fixed (byte* ptr = target)
            // {
            // var intPtr = new IntPtr(ptr);
            for (int i = 0; i < _format.SampleCount; i++)
            {
                var samplePtr = intPtr + i * _format.BytesPerSample * _format.Channels;

                for (int s = 0; s < _slots.Length; s++)
                {
                    var slot = _slots[s];
                    var d = dequedData[s];
                    if (success[s])
                    {
                        for (int ch = 0; ch < slot.Channels; ch++)
                        {
                            *(float*)(samplePtr + (slot.Offset + ch) * _format.BytesPerSample) +=
                                d[i * slot.Channels + ch];
                        }
                    }
                }
                // }
            }

            return true;
        }
    }

    public class ByteArrayMixBuffer : IMixBuffer
    {
        private byte[] _data;
        private AudioFormat OutputFormat { get; }

        public ByteArrayMixBuffer(string name, AudioFormat format)
        {
            OutputFormat = format;
            _data = new byte[format.BytesPerSample * format.BufferSize];
        }

        public byte[] Data => _data;


        public float[] GetFloatArray()
        {
            return MixBuffers.CopyByteToFloat(_data);
        }

        public unsafe void WriteChannel(int channel, float[] data)
        {
            if (data.Length != OutputFormat.SampleCount)
            {
                throw new Exception(
                    $"channel data length != OutputFormat.SampleCount, {data.Length}!={OutputFormat.SampleCount}");
            }

            if (OutputFormat.IsInterleaved)
            {
                fixed (byte* ptr = _data)
                {
                    var intPtr = new IntPtr(ptr);
                    intPtr += channel * OutputFormat.BytesPerSample;
                    for (int i = 0; i < OutputFormat.SampleCount; i++)
                    {
                        *((float*)intPtr) += data[i];
                        intPtr += OutputFormat.BytesPerSample * OutputFormat.Channels;
                    }
                }
            }
        }

        public unsafe void WriteChannelsInterleaved(int offset, int channels, float[] data)
        {
            if (data.Length < OutputFormat.SampleCount * channels)
            {
                throw new Exception(
                    $"channel data length != OutputFormat.SampleCount*channels, {data.Length}!={channels * OutputFormat.SampleCount}");
            }

            if (OutputFormat.IsInterleaved)
            {
                fixed (byte* ptr = _data)
                {
                    var intPtr = new IntPtr(ptr + offset * OutputFormat.BytesPerSample);
                    for (int i = 0; i < OutputFormat.SampleCount; i++)
                    {
                        for (int ch = 0; ch < channels; ch++)
                        {
                            *((float*)intPtr) += data[i * channels + ch];
                            intPtr += OutputFormat.BytesPerSample;
                        }

                        intPtr += OutputFormat.BytesPerSample * (OutputFormat.Channels - channels);
                    }
                }
            }
        }
    }

    /*public class AsioMixBuffer : IMixBuffer
    {
        private IntPtr[] _channels;
        public AsioMixBuffer(IntPtr[] channels)
        {
            _channels = channels;
        }

        public void Done(int channels)
        {
            throw new NotImplementedException();
        }

        public void WriteChannel(int channel, float[] data)
        {
            throw new NotImplementedException();
        }

        public void WriteChannelsInterleaved(int offset, int channels, float[] data)
        {
            throw new NotImplementedException();
        }
    }*/
}