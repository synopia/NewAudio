using System;
using System.Runtime.CompilerServices;

namespace NewAudio.Dsp
{
    public interface ISampleType{}
    public readonly struct Int16LsbSample: ISampleType{}
    public readonly struct Int24LsbSample: ISampleType{}
    public readonly struct Int32LsbSample: ISampleType{}
    public readonly struct Float32Sample: ISampleType{}

    public interface IConverter
    {
        unsafe void ConvertTo(float[] source, IntPtr target, int numFrames);
        unsafe void ConvertFrom(IntPtr source,float[] target, int numFrames);
        unsafe void ConvertTo(float[] source, IntPtr[] target, int numFrames, int startChannel, int numChannels);
        unsafe void ConvertFrom(IntPtr[] source,float[] target, int numFrames, int startChannel, int numChannels);
    }
    public sealed class Converter<TSampleType>: IConverter where TSampleType: struct,ISampleType
    {
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Write(byte* buf, float value)
        {
            if (typeof(TSampleType) == typeof(Float32Sample))
            {
                *(float*)buf = value;
                return 4;
            }
            if (typeof(TSampleType) == typeof(Int16LsbSample))
            {
                *(short*)buf = (short)(value * short.MaxValue);
                return 2;
            } 
            if (typeof(TSampleType) == typeof(Int24LsbSample))
            {
                const int normalizer = 8388607;
                var intValue = (int)(value * normalizer);
                *buf++ = (byte)(intValue & 255);
                *buf++ = (byte)((intValue>>8) & 255);
                *buf = (byte)((intValue>>16) & 255);
                return 3;
            } 
            if (typeof(TSampleType) == typeof(Int32LsbSample))
            {
                *(int*)buf = (int)(value * int.MaxValue);
                return 4;
            }
            throw new Exception();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Read(byte* buf, out float value)
        {
            if (typeof(TSampleType) == typeof(Float32Sample))
            {
                value = *(float*)buf;
                return 4;
            }
            if (typeof(TSampleType) == typeof(Int16LsbSample))
            {
                value = *(short*)buf / (float)short.MaxValue; 
                return 2;
            } 
            if (typeof(TSampleType) == typeof(Int24LsbSample))
            {
                const float normalizer = 8388607;
                var sample = *buf | (*(buf + 1) << 8) | ((sbyte)*(buf + 2) << 16);
                value = sample / normalizer;
                return 3;
            } 
            if (typeof(TSampleType) == typeof(Int32LsbSample))
            {
                value = *(int*)buf / (float)int.MaxValue; 
                return 4;
            }
            throw new Exception();
        }

        public unsafe void ConvertTo(float[] source, IntPtr target, int numFrames)
        {
            byte* t = (byte*)target.ToPointer();
            for (int i = 0; i < numFrames; i++)
            {
                t += Write(t, source[i]);
            }
        }
        public unsafe void ConvertFrom(IntPtr source,float[] target, int numFrames)
        {
            byte* s = (byte*)source.ToPointer();
            for (int i = 0; i < numFrames; i++)
            {
                s += Read(s, out target[i]);
            }
        }
        public unsafe void ConvertTo(float[] source, IntPtr[] target, int numFrames, int startChannel, int numChannels)
        {
            int offset = 0;
            for (int ch = startChannel; ch < numChannels; ch++)
            {
                byte* t = (byte*)target[ch].ToPointer();
                for (int i = 0; i < numFrames; i++)
                {
                    t += Write(t, source[offset++]);
                }
            }
        }
        public unsafe void ConvertFrom(IntPtr[] source,float[] target, int numFrames, int startChannel, int numChannels)
        {
            int offset = 0;
            for (int ch = startChannel; ch < numChannels; ch++)
            {
                byte* s = (byte*)source[ch].ToPointer();
                for (int i = 0; i < numFrames; i++)
                {
                    s += Read(s, out target[offset++]);
                }
            }
        }
    }
    public static class Converter
    {
        
        public static unsafe void Interleave(float[] source, byte[] interleaved, int frames, int channels, int framesToCopy )
        {
            fixed(byte* ptr= interleaved)
            {
                var intPtr = new IntPtr(ptr);
                
                for (int ch = 0; ch < channels; ch++)
                {
                    var s = new Span<float>(source, ch * frames, frames);
                    for (int i = 0, j = ch; i < framesToCopy; i++, j += channels)
                    {
                        *(float*)(intPtr+j*4) = s[i];
                    }
                }
            }
        }
    }
}