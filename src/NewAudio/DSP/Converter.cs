using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xt;

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
        void Read(XtBuffer source, int sourceStartFrame, AudioBuffer target, int targetStartFrame, int numFrames);
    }

    public interface IConvertWriter
    {
        void Write(AudioBuffer source, int sourceStartFrame, XtBuffer target, int targetStartFrame, int numFrames);
    }

    internal static class ConvertHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FrameSize<TSampleType>() where TSampleType: struct, ISampleType
        {
            if (typeof(TSampleType) == typeof(Float32Sample))
            {
                return 4;
            } 
            
            if (typeof(TSampleType) == typeof(Int16LsbSample))
            {
                return 2;
            } 
            if (typeof(TSampleType) == typeof(Int24LsbSample))
            {
                return 3;
            } 
            if (typeof(TSampleType) == typeof(Int32LsbSample))
            {
                return 4;
            }

            throw new NotImplementedException();
        }
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
                *(int*)buf = (int)((double)value * int.MaxValue);
                return 4;
            }

            throw new Exception();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteInterleaved(AudioBuffer source, int sourceStartFrame, XtBuffer target, int targetStartFrame, int numFrames)
        {
            var targetOffset = ConvertHelper.FrameSize<TSampleType>()*source.NumberOfChannels*targetStartFrame;
            for (int i = sourceStartFrame; i < sourceStartFrame+numFrames; i++)
            {
                for (int ch = 0; ch < source.NumberOfChannels; ch++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    targetOffset += Write(&(((byte*)target.output)[targetOffset]), source[ch*source.NumberOfFrames+i]);
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteNonInterleaved(AudioBuffer source, int sourceStartFrame, XtBuffer target, int targetStartFrame, int numFrames)
        {
            if (typeof(TSampleType) == typeof(Float32Sample))
            {
                for (int ch = 0; ch <source.NumberOfChannels ; ch++)
                {
                    new Span<float>(source.Data, sourceStartFrame+ch*source.NumberOfFrames, numFrames).CopyTo(
                        // ReSharper disable once PossibleNullReferenceException
                        new Span<float>(&((float**)target.output)[ch][targetStartFrame], numFrames));
                }
                return;
            }

            for (int ch = 0; ch < source.NumberOfChannels; ch++)
            {
                var targetOffset = ConvertHelper.FrameSize<TSampleType>()*targetStartFrame;
                
                for (int i = sourceStartFrame; i < sourceStartFrame+numFrames; i++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    targetOffset += Write(&((byte**)target.output)[ch][targetOffset], source[i+ch*source.NumberOfFrames]);
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(AudioBuffer source, int sourceStartFrame, XtBuffer target, int targetStartFrame, int numFrames)
        {
            Trace.Assert(sourceStartFrame+numFrames<=source.NumberOfFrames);
            Trace.Assert(targetStartFrame+numFrames<=target.frames);
            
            if (typeof(TMemoryAccess) == typeof(Interleaved))
            {
                WriteInterleaved(source,sourceStartFrame, target, targetStartFrame, numFrames);
            }
            else
            {
                WriteNonInterleaved(source, sourceStartFrame,target, targetStartFrame,numFrames);
            }
        }
    }

    public sealed class ConvertReader<TSampleType, TMemoryAccess> : IConvertReader
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        const double Int32ToFloat=1.0 / 2147483648.0;
        const float Int24ToFloat=1.0f / 8388607.0f;
        const float Int16ToFloat=1.0f / short.MaxValue;
        
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
                value = *(short*)buf * Int16ToFloat;
                return 2;
            }

            if (typeof(TSampleType) == typeof(Int24LsbSample))
            {
                var sample = *buf | (*(buf + 1) << 8) | ((sbyte)*(buf + 2) << 16);
                value = sample * Int16ToFloat;
                return 3;
            }

            if (typeof(TSampleType) == typeof(Int32LsbSample))
            {
                value = (float) (*(int*)buf * Int32ToFloat);
                return 4;
            }

            throw new Exception();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void ReadInterleaved(XtBuffer source, int sourceStartFrame, AudioBuffer target, int targetStartFrame, int numFrames)
        {
            int offset = ConvertHelper.FrameSize<TSampleType>()*sourceStartFrame*target.NumberOfChannels;
            for (int i = targetStartFrame; i < targetStartFrame+numFrames; i++)
            {
                for (int ch = 0; ch < target.NumberOfChannels; ch++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    offset += Read(&((byte*)source.input)[offset], out target.Data[ch*target.NumberOfFrames+i]);
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void ReadNonInterleaved(XtBuffer source, int sourceStartFrame, AudioBuffer target, int targetStartFrame, int numFrames)
        {
            if (typeof(TSampleType) == typeof(Float32Sample))
            {
                for (int ch = 0; ch < target.NumberOfChannels; ch++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    new Span<float>(&((float**)source.input)[ch][sourceStartFrame], numFrames).CopyTo(
                        new Span<float>(target.Data, targetStartFrame+ch*target.NumberOfFrames, numFrames));
                }
                return;
            }
            
            for (int ch = 0; ch < target.NumberOfChannels; ch++)
            {
                int sourceOffset = ConvertHelper.FrameSize<TSampleType>()*sourceStartFrame;
                for (int i = targetStartFrame; i < targetStartFrame+numFrames; i++)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    sourceOffset += Read(&((byte**)source.input)[ch][sourceOffset], out target.Data[i+ch*target.NumberOfFrames]);
                }
            }
        }
        public void Read(XtBuffer source, int sourceStartFrame, AudioBuffer target, int targetStartFrame, int numFrames)
        {
            if (typeof(TMemoryAccess) == typeof(Interleaved))
            {
                ReadInterleaved(source, sourceStartFrame,target,targetStartFrame, numFrames);
            }
            else
            {
                ReadNonInterleaved(source, sourceStartFrame,target, targetStartFrame,numFrames);
            }
        }
    }
}