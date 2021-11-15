using System;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using NewAudio.Core;
using VL.Lib.Adaptive;

namespace NewAudio.Internal
{
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
        private Barrier _barrier;
        private int _write;
        private int _read;
        private EventWaitHandle _readerWait;
        
        public MixBuffers(int devices, int bufferCount, AudioFormat format)
        {
            _buffers = new IMixBuffer[bufferCount];
            for (int i = 0; i < bufferCount; i++)
            {
                _buffers[i] = new ByteArrayMixBuffer("Buf " + i, format);
            }
            _readerWait = new AutoResetEvent(false);
            _barrier = new Barrier(devices+1, barrier =>
            {
                _read = _write;
                _write = 1 - _write;
                _readerWait.Set();
            });
        }

        public  static unsafe float[] CopyByteToFloat(byte[] buf)
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
        public  static unsafe byte[] CopyFloatToByte(float[] buf)
        {
            var result = new byte[buf.Length * 4];
            fixed (byte* ptr = result)
            {
                var intPtr = new IntPtr(ptr);
                Marshal.Copy(buf, 0, intPtr, buf.Length);                
            }
            
            return result;
        }
        
        public IMixBuffer GetWriteBuffer(CancellationToken cancellationToken)
        {
            _barrier.SignalAndWait(cancellationToken);
            return !cancellationToken.IsCancellationRequested ? _buffers[_write] : null;
        }

        public int ReadPlayBuffer(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var buf = GetReadBuffer(cancellationToken);
            if (buf != null)
            {
                Array.Copy(buf.Data, 0, buffer, offset, count);
                return count-offset;
            }

            return 0;
        }
        
        public IMixBuffer GetReadBuffer(CancellationToken cancellationToken)
        {
            _barrier.SignalAndWait(cancellationToken);
            // var r = _readerWait.WaitOne(1);
            
            return !cancellationToken.IsCancellationRequested ? _buffers[_read] : null;
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
                throw new Exception($"channel data length != OutputFormat.SampleCount, {data.Length}!={OutputFormat.SampleCount}");
            }

            if (OutputFormat.IsInterleaved)
            {
                fixed (byte* ptr = _data)
                {
                    var intPtr = new IntPtr(ptr);
                    intPtr += channel * OutputFormat.BytesPerSample;
                    for (int i = 0; i < OutputFormat.SampleCount; i++)
                    {
                        *((float*)intPtr) = data[i];
                        intPtr += OutputFormat.BytesPerSample * OutputFormat.Channels;
                    }
                }
            }
        }
        
        public unsafe void WriteChannelsInterleaved(int offset, int channels, float[] data)
        {
            if (data.Length < OutputFormat.SampleCount*channels)
            {
                throw new Exception($"channel data length != OutputFormat.SampleCount*channels, {data.Length}!={channels*OutputFormat.SampleCount}");
            }

            if (OutputFormat.IsInterleaved)
            {
                if (channels == OutputFormat.Channels)
                {
                    fixed (byte* ptr = _data)
                    {
                        var intPtr = new IntPtr(ptr+offset * OutputFormat.BytesPerSample);
                        Marshal.Copy(data, 0, intPtr, OutputFormat.BufferSize);
                    }
                }
                else
                {
                    fixed (byte* ptr = _data)
                    {
                        var intPtr = new IntPtr(ptr + offset * OutputFormat.BytesPerSample);
                        for (int i = 0; i < OutputFormat.SampleCount; i++)
                        {
                            for (int ch = 0; ch < channels; ch++)
                            {
                                *((float*)intPtr) = data[i*channels+ch];
                                intPtr += OutputFormat.BytesPerSample;
                            }
                            intPtr += OutputFormat.BytesPerSample * (OutputFormat.Channels-channels);
                        }
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