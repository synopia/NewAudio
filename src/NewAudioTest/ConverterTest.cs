using System;
using System.Linq;
using FFTW.NET;
using NewAudio.Dsp;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class ConverterTest
    {
        [Test]
        public void TestWriterFloat32()
        {
            var writerInterleaved = new ConvertWriter<Float32Sample, Interleaved>();
            var writerNonInterleaved = new ConvertWriter<Float32Sample, NonInterleaved>();
            float[] source = { 10, 11, 12, 20, 21, 22, 30, 31, 32 };
            float[] target = new float[3 * 3];
            using var pin = new PinnedArray<float>(target);
            writerInterleaved.Write(source, pin.Pointer, 3,  3);
            Assert.AreEqual(new float[]{10,20,30, 11,21,31, 12,22,32 }, target);
            
            writerNonInterleaved.Write(source, pin.Pointer, 3,  3);
            Assert.AreEqual(new float[] { 10, 11, 12, 20, 21, 22, 30, 31, 32 }, target);
        }
        [Test]
        public void TestReaderFloat32()
        {
            var readerInterleaved = new ConvertReader<Float32Sample, Interleaved>();
            var readerNonInterleaved = new ConvertReader<Float32Sample, NonInterleaved>();
            float[] source = { 10, 11, 12, 20, 21, 22, 30, 31, 32 };
            float[] target = new float[3*3];
            using var pin = new PinnedArray<float>(source);
            readerInterleaved.Read(pin.Pointer, target,3,  3);
            Assert.AreEqual(new float[]{10,20,30, 11,21,31, 12,22,32 }, target);
            
            readerNonInterleaved.Read(pin.Pointer, target, 3,  3);
            Assert.AreEqual(new float[] { 10, 11, 12, 20, 21, 22, 30, 31, 32 }, target);
        }
        [Test]
        public unsafe void TestWriterInt32()
        {
            var writerInterleaved = new ConvertWriter<Int32LsbSample, Interleaved>();
            var writerNonInterleaved = new ConvertWriter<Int32LsbSample, NonInterleaved>();
            float[] source = { 0.1f, 0.11f, 0.12f, 0.20f, 0.21f, 0.22f, 0.30f, 0.31f, 0.32f };
            int[] target = new int[3*3];
            fixed (void* t = target)
            {
                writerNonInterleaved.Write(source, new IntPtr(t), 3, 3);
                var expected = source.Select(s => (int)(s * int.MaxValue)).ToArray();
                Assert.AreEqual(expected, target);
                
                float[] s2 = { 0.1f, 0.2f, 0.3f, 0.11f, 0.21f, 0.31f, 0.12f, 0.22f, 0.32f };
                var e2 = source.Select(s => (int)(s * int.MaxValue)).ToArray();

                writerInterleaved.Write(s2, new IntPtr(t), 3,  3);
                Assert.AreEqual(e2, target);
            }

           
        }
        [Test]
        public unsafe void TestReaderInt32()
        {
            var readerInterleaved = new ConvertReader<Int32LsbSample, Interleaved>();
            var readerNonInterleaved = new ConvertReader<Int32LsbSample, NonInterleaved>();
            //                   0         1          2     3         4     5        6        7      9
            int[] source = { int.MaxValue, 0, int.MinValue, 0, int.MaxValue, 0,  int.MinValue, 0, int.MaxValue };
            float[] target = new float[3 * 3];
            fixed (void* s = source)
            {
                readerInterleaved.Read(new IntPtr(s), target, 3, 3);
                Assert.AreEqual(new float[] { 1f, 0, -1f, 0, 1f, 0, -1f, 0, 1f }, target);

                readerNonInterleaved.Read(new IntPtr(s), target, 3, 3);
                Assert.AreEqual(new float[] { 1f, 0, -1f, 0, 1f, 0, -1f, 0, 1f }, target);
            }
        }
    }
}