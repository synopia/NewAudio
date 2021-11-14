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
        private bool[] _usingBuffer;
        private bool[] _doneBuffer;
        private IMixBuffer[] Buffers;
        public int PlayBuffer;
        public int WriteBuffer;
        public EventWaitHandle ReadHandle;
        public EventWaitHandle[] WriteHandles;
        
        public MixBuffers(int devices, int bufferCount, AudioFormat format)
        {
            _usingBuffer = new bool[devices];
            _doneBuffer = new bool[devices];
            WriteHandles = new EventWaitHandle[devices];
            Buffers = new IMixBuffer[bufferCount];
            for (int i = 0; i < bufferCount; i++)
            {
                Buffers[i] = new ByteArrayMixBuffer("Ch" + i, format);
            }

            PlayBuffer = 0;
            WriteBuffer = 0;
            
            ReadHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            for (int i = 0; i < devices; i++)
            {
                WriteHandles[i] = new EventWaitHandle(true, EventResetMode.AutoReset);                
            }
        }

        public unsafe static float[] CopyByteToFloat(byte[] buf)
        {
            var result = new float[buf.Length / 4];
            fixed (float* ptr = result)
            {
                var intPtr = new IntPtr(ptr);
                Marshal.Copy(buf, 0, intPtr, buf.Length);                
            }
            
            return result;
        }
        public unsafe static byte[] CopyFloatToByte(float[] buf)
        {
            var result = new byte[buf.Length * 4];
            fixed (byte* ptr = result)
            {
                var intPtr = new IntPtr(ptr);
                Marshal.Copy(buf, 0, intPtr, buf.Length);                
            }
            
            return result;
        }
        
        public IMixBuffer GetMixBuffer(int index)
        {
            WriteHandles[index].WaitOne();
            
            _usingBuffer[index] = true;
            _doneBuffer[index] = false;
            return Buffers[WriteBuffer];
        }

        public void ReturnMixBuffer(int index)
        {
            lock (_usingBuffer)
            {
                _usingBuffer[index] = false;
                _doneBuffer[index] = true;
                if (!_usingBuffer.Any(b => b) &&_doneBuffer.All(b=>b))
                {
                    for (var i = 0; i < _doneBuffer.Length; i++)
                    {
                        _doneBuffer[i] = false;
                    }

                    WriteBuffer++;
                    WriteBuffer %= Buffers.Length;
                    ReadHandle.Set();
                }
            }
        }

        public void ReadPlayBuffer(byte[] buffer, int offset, int count)
        {
            var buf = GetPlayBuffer();
            Array.Copy(buf.Data, 0, buffer, offset, count);
        }
        
        public IMixBuffer GetPlayBuffer()
        {
            ReadHandle.WaitOne();
            
            return Buffers[PlayBuffer];
        }
        public void DonePlaying()
        {
            PlayBuffer++;
            PlayBuffer %= Buffers.Length;
            for (int i = 0; i < WriteHandles.Length; i++)
            {
                WriteHandles[i].Set();
            }
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
                    // byte* ptr = (byte*)_data[0];
                    // ptr += channel * OutputFormat.BytesPerSample;
                    // for (int i = 0; i < OutputFormat.SampleCount; i++)
                    // {
                        // *((float*)ptr) = channelData[i];
                        // ptr += OutputFormat.BytesPerSample * OutputFormat.Channels;
                    // }
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