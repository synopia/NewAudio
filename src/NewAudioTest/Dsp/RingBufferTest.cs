﻿using System;
using System.Threading.Tasks;
using NUnit.Framework;
using NewAudio.Dsp;

namespace NewAudioTest.Dsp
{
    [TestFixture]
    public class RingBufferTest
    {

        [Test]
        public void TestReadAll()
        {
            RingBuffer<int> buf = new RingBuffer<int>(100);

            int[] a = new int[buf.Size];
            int[] b = new int[buf.Size];

            for (int i = 0; i < buf.Size; i++)
            {
                a[i] = 1000 + i;
            }

            buf.Write(a, a.Length);
            buf.Read(b, b.Length);
            
            for (int i = 0; i < buf.Size; i++)
            {
                Assert.AreEqual(1000 + i, b[i]);
            }

        }

        [Test]
        public void TestThreaded()
        {
            var rb = new RingBuffer<int>(100);
            var numReads = 10000;
            var readBufferSize = 511;
            var writeBufferSize = 493;
            var ticker = 0;
            var currReads = 0;

            var reader = Task.Run(() =>
            {
                int[] buf = new int[readBufferSize];
                var current = 0;
                while (currReads < numReads)
                {
                    var avail = rb.AvailableRead;
                    if (avail > 0)
                    {
                        var count = Math.Min(avail, buf.Length);
                        rb.Read(buf, count);
                        for (int i = 0; i < count; i++)
                        {
                            Assert.AreEqual(current++, buf[i]);
                        }
                    }

                    currReads++;
                }
            });
            var writer = Task.Run(() =>
            {
                int[] buf = new int[writeBufferSize];
                var current = 0;
                while (currReads < numReads)
                {
                    var avail = rb.AvailableWrite;
                    if (avail > 0)
                    {
                        var count = Math.Min(avail, buf.Length);
                        for (int i = 0; i < count; i++)
                        {
                            buf[i] = current++;
                        }

                        rb.Write(buf, count);
                    }

                }
            });

            Task.WaitAll(new[] { reader, writer });
        }
    }
}