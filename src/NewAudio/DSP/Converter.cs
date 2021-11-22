using System;
using System.Runtime.CompilerServices;

namespace NewAudio.Dsp
{
    public interface ISampleType
    {
    }

    public interface IMemoryAccess
    {
    }

    public readonly struct Int16LsbSample : ISampleType
    {
    }

    public readonly struct Int24LsbSample : ISampleType
    {
    }

    public readonly struct Int32LsbSample : ISampleType
    {
    }

    public readonly struct Float32Sample : ISampleType
    {
    }

    public readonly struct Interleaved : IMemoryAccess
    {
    }

    public readonly struct NonInterleaved : IMemoryAccess
    {
    }

    public interface IConvertReader
    {
        void Read(IntPtr source, float[] target, int numFrames, int numChannels);
    }

    public interface IConvertWriter
    {
        void Write(float[] source, IntPtr target, int numFrames, int numChannels);
    }

    public sealed class ConvertWriter<TSampleType, TMemoryAccess> : IConvertWriter
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int Write(byte* buf, float value)
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
                *buf++ = (byte)((intValue >> 8) & 255);
                *buf = (byte)((intValue >> 16) & 255);
                return 3;
            }

            if (typeof(TSampleType) == typeof(Int32LsbSample))
            {
                *(int*)buf = (int)(value * int.MaxValue);
                return 4;
            }

            throw new Exception();
        }

        private unsafe void WriteInterleaved(float[] source, IntPtr target, int numFrames, int numChannels)
        {
            var offset = 0;
            for (int i = 0; i < numFrames; i++)
            {
                for (int ch = 0; ch < numChannels; ch++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    offset += Write(&(((byte*)target)[offset]), source[ch*numFrames+i]);
                }
            }
        }
        private unsafe void WriteNonInterleaved(float[] source, IntPtr target, int numFrames, int numChannels)
        {
            int offset = 0;
            var targetOffset = 0;
            for (int ch = 0; ch < numChannels; ch++)
            {
                for (int i = 0; i < numFrames; i++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    targetOffset += Write(&((byte*)target)[targetOffset], source[offset++]);
                }
            }
        }

        public unsafe void Write(float[] source, IntPtr target, int numFrames, int numChannels)
        {
            if (typeof(TMemoryAccess) == typeof(Interleaved))
            {
                WriteInterleaved(source, target, numFrames, numChannels);
            }
            else
            {
                if (typeof(TSampleType) == typeof(Float32Sample))
                {
                    new Span<float>(source,0, numFrames*numChannels).CopyTo(new Span<float>((void*)target, numFrames*numChannels));                    
                }
                else
                {
                    WriteNonInterleaved(source, target, numFrames, numChannels);
                }
            }
        }
    }

    public sealed class ConvertReader<TSampleType, TMemoryAccess> : IConvertReader
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int Read(byte* buf, out float value)
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


        private unsafe void ReadInterleaved(IntPtr source, float[] target, int numFrames, int numChannels)
        {
            int offset = 0;
            for (int i = 0; i < numFrames; i++)
            {
                for (int ch = 0; ch < numChannels; ch++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    offset += Read(&((byte*)source)[offset], out target[ch*numFrames+i]);
                }
            }
        }
        private unsafe void ReadNonInterleaved(IntPtr source, float[] target, int numFrames,int numChannels)
        {
            int offset = 0;
            int sourceOffset = 0;
            for (int ch = 0; ch < numChannels; ch++)
            {
                for (int i = 0; i < numFrames; i++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    sourceOffset += Read(&((byte*)source)[sourceOffset], out target[offset++]);
                }
            }
        }
        public unsafe void Read(IntPtr source, float[] target, int numFrames, int numChannels)
        {
            if (typeof(TMemoryAccess) == typeof(Interleaved))
            {
                ReadInterleaved(source, target, numFrames, numChannels);
            }
            else
            {
                if (typeof(TSampleType) == typeof(Float32Sample))
                {
                    new Span<float>((void*)source, numChannels*numFrames).CopyTo(new Span<float>(target, 0, numFrames*numChannels));
                }
                else
                {
                    ReadNonInterleaved(source, target, numFrames, numChannels);
                }
            }
        }
    }
}