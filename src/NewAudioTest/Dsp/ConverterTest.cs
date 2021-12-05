using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using FFTW.NET;
using NewAudio.Dsp;
using NewAudio.Internal;
using NUnit.Framework;
using Xt;

namespace NewAudioTest.Dsp
{
    public class ConverterTestData
    {
        private const float MaxValue = 100f;

        public static float[] Float32(params int[] data)
        {
            return data.Select(d=>(float)(d/MaxValue)).ToArray();
        }
        public static int[] Int32(params int[] data)
        {
            return data.Select(d=>(int)((d/MaxValue)*(double)int.MaxValue)).ToArray();
        }
        public static short[] Int16(params int[] data)
        {
            return data.Select(d=>(short)((d/MaxValue)*short.MaxValue)).ToArray();
        }
        
        public static IEnumerable TestCases
        {
            get
            {
                // full copy
                yield return new TestFixtureData(typeof(Float32Sample), typeof(Interleaved), 5, 2, 0, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), Float32(1,6, 2,7, 3,8, 4,9, 5,10), null);
                yield return new TestFixtureData(typeof(Int32LsbSample), typeof(Interleaved), 5, 2, 0, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), Int32(1,6, 2,7, 3,8, 4,9, 5,10), null);
                yield return new TestFixtureData(typeof(Int16LsbSample), typeof(Interleaved), 5, 2, 0, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), Int16(1,6, 2,7, 3,8, 4,9, 5,10), null);

                // copy to offset
                yield return new TestFixtureData(typeof(Float32Sample), typeof(Interleaved), 2, 2, 0, 3,
                    Float32(1,2,3,4,5,6,7,8,9,10), Float32(0,0,0,0,0,0, 1,6,2,7), 
                    Float32(1,2,0,0,0,6,7,0,0,0));
                yield return new TestFixtureData(typeof(Int32LsbSample), typeof(Interleaved), 2, 2, 0, 3,
                    Float32(1,2,3,4,5,6,7,8,9,10), Int32(0,0,0,0,0,0, 1,6,2,7), 
                    Float32(1,2,0,0,0,6,7,0,0,0));
                yield return new TestFixtureData(typeof(Int16LsbSample), typeof(Interleaved), 2, 2, 0, 3,
                    Float32(1,2,3,4,5,6,7,8,9,10), Int16(0,0,0,0,0,0, 1,6,2,7), 
                    Float32(1,2,0,0,0,6,7,0,0,0));

                // copy from offset
                yield return new TestFixtureData(typeof(Float32Sample), typeof(Interleaved), 2, 2, 3, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), Float32(4,9,5,10,0,0, 0,0,0,0), 
                    Float32(0,0,0,4,5,0,0,0,9,10));
                yield return new TestFixtureData(typeof(Int32LsbSample), typeof(Interleaved), 2, 2, 3, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), Int32(4,9,5,10,0,0, 0,0,0,0), 
                    Float32(0,0,0,4,5,0,0,0,9,10));
                yield return new TestFixtureData(typeof(Int16LsbSample), typeof(Interleaved), 2, 2, 3, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), Int16(4,9,5,10,0,0, 0,0,0,0), 
                    Float32(0,0,0,4,5,0,0,0,9,10));

                // copy mid
                yield return new TestFixtureData(typeof(Float32Sample), typeof(Interleaved), 3, 2, 1, 1,
                    Float32(1,2,3,4,5,6,7,8,9,10), Float32(0,0, 2,7, 3,8, 4,9, 0,0), 
                    Float32(0,2,3,4,0,0,7,8,9,0));
                yield return new TestFixtureData(typeof(Int32LsbSample), typeof(Interleaved), 3, 2, 1, 1,
                    Float32(1,2,3,4,5,6,7,8,9,10), Int32(0,0, 2,7, 3,8, 4,9, 0,0),
                    Float32(0,2,3,4,0,0,7,8,9,0));
                yield return new TestFixtureData(typeof(Int16LsbSample), typeof(Interleaved), 3, 2, 1, 1,
                    Float32(1,2,3,4,5,6,7,8,9,10), Int16(0,0, 2,7, 3,8, 4,9, 0,0),
                    Float32(0,2,3,4,0,0,7,8,9,0));

                // full copy
                yield return new TestFixtureData(typeof(Float32Sample), typeof(NonInterleaved), 5, 2, 0, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10),
                    new [] { Float32(1,2,3,4,5), Float32(6,7,8,9,10) }, null);
                yield return new TestFixtureData(typeof(Int32LsbSample), typeof(NonInterleaved), 5, 2, 0, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10),
                    new [] { Int32(1,2, 3,4,5), Int32(6,7,8,9,10) }, null);
                yield return new TestFixtureData(typeof(Int16LsbSample), typeof(NonInterleaved), 5, 2, 0, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10),
                    new [] { Int16(1,2, 3,4,5), Int16(6,7,8,9,10) }, null);
                
                // copy to offset
                yield return new TestFixtureData(typeof(Float32Sample), typeof(NonInterleaved), 2, 2, 0, 3,
                    Float32(1,2,3,4,5,6,7,8,9,10),
                    new [] { Float32(0,0,0,1,2), Float32(0,0,0,6,7) }, 
                    Float32(1,2,0,0,0,6,7,0,0,0));
                yield return new TestFixtureData(typeof(Int32LsbSample), typeof(NonInterleaved), 2, 2, 0, 3,
                    Float32(1,2,3,4,5,6,7,8,9,10),
                    new [] { Int32(0,0,0,1,2), Int32(0,0,0,6,7) }, 
                    Float32(1,2,0,0,0,6,7,0,0,0));
                yield return new TestFixtureData(typeof(Int16LsbSample), typeof(NonInterleaved), 2, 2, 0, 3,
                    Float32(1,2,3,4,5,6,7,8,9,10),
                    new [] { Int16(0,0,0,1,2), Int16(0,0,0,6,7) }, 
                    Float32(1,2,0,0,0,6,7,0,0,0));
                // copy from offset
                yield return new TestFixtureData(typeof(Float32Sample), typeof(NonInterleaved), 2, 2, 3, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), 
                    new [] { Float32(4,5,0,0,0), Float32(9,10,0,0,0) },
                    Float32(0,0,0,4,5,0,0,0,9,10));
                yield return new TestFixtureData(typeof(Int32LsbSample), typeof(NonInterleaved), 2, 2, 3, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), 
                    new [] { Int32(4,5,0,0,0), Int32(9,10,0,0,0) },
                    Float32(0,0,0,4,5,0,0,0,9,10));
                yield return new TestFixtureData(typeof(Int16LsbSample), typeof(NonInterleaved), 2, 2, 3, 0,
                    Float32(1,2,3,4,5,6,7,8,9,10), 
                    new [] { Int16(4,5,0,0,0), Int16(9,10,0,0,0) },
                    Float32(0,0,0,4,5,0,0,0,9,10));
                // copy mid
                yield return new TestFixtureData(typeof(Float32Sample), typeof(NonInterleaved), 3, 2, 1, 1,
                    Float32(1,2,3,4,5,6,7,8,9,10), 
                    new [] { Float32(0,2,3,4,0), Float32(0,7,8,9,0) }, 
                    Float32(0,2,3,4,0,0,7,8,9,0));
                yield return new TestFixtureData(typeof(Int32LsbSample), typeof(NonInterleaved), 3, 2, 1, 1,
                    Float32(1,2,3,4,5,6,7,8,9,10), 
                    new [] { Int32(0,2,3,4,0), Int32(0,7,8,9,0) }, 
                    Float32(0,2,3,4,0,0,7,8,9,0));
                yield return new TestFixtureData(typeof(Int16LsbSample), typeof(NonInterleaved), 3, 2, 1, 1,
                    Float32(1,2,3,4,5,6,7,8,9,10), 
                    new [] { Int16(0,2,3,4,0), Int16(0,7,8,9,0) }, 
                    Float32(0,2,3,4,0,0,7,8,9,0));

            }
        }
    }

    [TestFixtureSource(typeof(ConverterTestData), nameof(ConverterTestData.TestCases))]
    public class ConverterTest
    {
        private int _numFrames;
        private int _framesToCopy;
        private int _numChannels;
        private AudioBuffer _audioBuffer;
        private Array _expected;
        private Array _expected2;
        private IConvertWriter _writer;
        private IConvertReader _reader;
        private Type _sampleType;
        private int _startSource;
        private int _startTarget;
        public ConverterTest(Type sampleType, Type memoryType, int numFrames, int numChannels, int startSource, int startTarget, float[] data,
            Array expected, Array expected2 = null)
        {
            _expected2 = expected2;
            _startSource = startSource;
            _startTarget = startTarget;
            _sampleType = sampleType;
            _expected = expected;
            _numFrames = data.Length/numChannels;
            _framesToCopy = numFrames;
            _numChannels = numChannels;
            _audioBuffer = new AudioBuffer( numChannels, _numFrames);
            for (int ch = 0; ch < numChannels; ch++)
            {
                for (int i = 0; i < _numFrames; i++)
                {
                    _audioBuffer[ch, i] = data[ch * _numFrames + i];
                }
            }
            
            var realType = typeof(ConvertWriter<,>).MakeGenericType(new Type[] { sampleType, memoryType });
            _writer = (IConvertWriter)Activator.CreateInstance(realType);
            realType = typeof(ConvertReader<,>).MakeGenericType(new Type[] { sampleType, memoryType });
            _reader = (IConvertReader)Activator.CreateInstance(realType);
        }

        private void Run(Action<IntPtr, Array> action)
        {
            Array actual = null;
            var elementType = _expected.GetType().GetElementType();
            IntPtr ptr;
            GCHandle gch = default;
            PointerPointer pp = null;
            if (elementType?.IsArray ?? false)
            {
                pp = new PointerPointer(elementType.GetElementType(), _numChannels, _numFrames);
                actual = pp.Data;
                ptr = pp.Pointer;
            }
            else
            {
                actual = Array.CreateInstance(elementType, _numChannels * _numFrames);
                gch = GCHandle.Alloc(actual, GCHandleType.Pinned);
                ptr = gch.AddrOfPinnedObject();
            }

            try
            {
                action(ptr, actual);
            }
            finally
            {
                if (gch.IsAllocated)
                {
                    gch.Free();
                }

                pp?.Dispose();
            }
        }
        
        [Test]
        public void TestWriteRead()
        {
            Run((ptr, actual) =>
            {
                XtBuffer buffer = new XtBuffer()
                {
                    frames = _numFrames,
                    output = ptr
                };

                _writer.Write(_audioBuffer, _startSource, buffer, _startTarget, _framesToCopy);
                
                Assert.AreEqual(_expected, actual, "Written data incorrect");

                AudioBuffer actual2 = new AudioBuffer( _numChannels, _numFrames);
                buffer = new XtBuffer()
                {
                    frames = _numFrames,
                    input = ptr
                };
                _reader.Read(buffer, _startTarget, actual2, _startSource, _framesToCopy);
                
                Assert.AreEqual(_audioBuffer.Size, actual2.Size, "Read data incorrect");
                /*
                for (int i = 0; i < _audioBuffer.Size; i++)
                {
                    if (_expected2 == null)
                    {
                        Assert.AreEqual(_audioBuffer[0, i], actual2[i], 1.0 / short.MaxValue);
                    }
                    else
                    {
                        Assert.AreEqual((float)_expected2.GetValue(i), actual2[i], 1.0 / short.MaxValue);
                        
                    }
                }
            */
            });
        }
   
    }
}