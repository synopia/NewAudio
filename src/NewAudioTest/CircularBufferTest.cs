using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Internal;
using NUnit.Framework;
using Serilog;
using Serilog.Formatting.Display;
using SharedMemory;

namespace NewAudioTest
{

    [TestFixture]
    public class CircularBufferTest
    {
        private ILogger _logger;

        private void Init()
        {
            _logger = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .WriteTo.Seq("http://localhost:5341")
                .WriteTo.File("VL.NewAudio.log",
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
                .MinimumLevel.Verbose()
                .CreateLogger();
            Log.Logger = _logger;

        }

        [Test]
        public void ManyBufs()
        {
            Init();
            var b = new CircularBuffer("1", 20, 2048);
            byte[] bb = new byte[3000];
            byte[] bb2 = new byte[2048];
            var b11 = new CircularBuffer("1");
            var b21 = new CircularBuffer("1");
            // b21.Read(bb);
            var a1= b.Write(bb);
            var a2 = a1+b.Write(bb, a1);
            var a3 = a2+b.Write(bb, a2);
            var r1 = b11.Read(bb2);
            var b1 = b.ReadNodeHeader().ReadEnd;
            var r2 = b11.Read(bb2);
            var b2 = b.ReadNodeHeader().ReadEnd;
            var r3 = b11.Read(bb2);
            var b3 = b.ReadNodeHeader().ReadEnd;
            var r4 = b21.Read(bb2);
            var b4 = b.ReadNodeHeader().ReadEnd;
            var a4= b.Write(bb);
            // b11.Read(bb);
            Log.Information("{a1} {a2} {a3} {a4} {b1} {b2} {b3} {b4} ",a1,a2,a3,a4, b1,b2,b3,b4 );
            Log.Information("{r0} {r1} {r2} {r3}",r1, r2, r3, r4);

        }

        [Test]
        public void Test3()
        {
            var format = new AudioFormat(48000, 256, 2, true);
            var buffer = new CircularBuffer("X", 256, format.BufferSize);
            var buffer2 = new CircularBuffer("X");
            float[] f = new float[format.BufferSize];
            for (int i = 0; i < format.BufferSize; i++)
            {
                f[i] = i;
            }
             var wrote = buffer.Write(f);

            float[] f2 = new float[format.BufferSize];
            var read = buffer2.Read(f2);

            Thread.Sleep(100);
            read = buffer2.Read(f2, read);
            
            _logger.Information("{wrote} {read} {f1} {f2}",read, wrote, f, f2);
        }

        [Test]
        public void TestSimple()
        {
            Init();
            int writerTotal = 100000;
            int writer = 1;
            int writerSize = writerTotal;
            int readerSize = writerTotal;

            int writerChunkSize = 100;
            int readerChunkSize = 100;
            var bufSize = 1000;
            // var buffer = new CircularSampleBuffer(bufSize);
            // var buffer = new LockFreeCircularBuffer(bufSize);
            var buffer = new CircularBuffer("Test", bufSize, 4*writerChunkSize);
            var tasks = new List<Task>();
            for (int i = 0; i < writer; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var local = new float[writerChunkSize];
                    for (int j = 0; j < writerChunkSize; j++)
                    {
                        local[j] = i + 1.0f;
                    }
                    while (writerSize > 0)
                    {
                        // if (buffer.Capacity - buffer.Count > 0)
                        // {
                            var written = buffer.Write(local, 0, Math.Min(writerSize,writerChunkSize));
                            _logger.Information("Wrote {s}", written);
                            writerSize -= written;
                            if (writerSize <= 0)
                            {
                                _logger.Information("????");
                            }
                            if (written < writerChunkSize)
                            {
                                _logger.Information($"Overflow {writerChunkSize - written}");
                            }

                            if (written == 0)
                            {
                                Task.Delay(1).Wait();

                            }
                        // }
                        // else
                        // {
                        // }
                    }
                }
                    )
                    );
            }

            tasks.Add(Task.Run(() =>
            {
                var local = new float[readerChunkSize];
                while (readerSize > 0)
                {
                    // if (buffer.Count > 0)
                    // {
                        var read = buffer.Read(local, 0, readerChunkSize);
                        _logger.Information("Read {read}", read);
                        readerSize -= read;
                        for (int j = 0; j < read; j++)
                        {
                            Assert.IsTrue(local[j] > 0);
                        }

                        if (read < readerChunkSize)
                        {
                            _logger.Information($"Underflow {readerChunkSize - read}");
                        }

                        if (read == 0)
                        {
                            Task.Delay(1).Wait();

                        }
                    // }
                }
            }));

            Task.WaitAll(tasks.ToArray());
        }
    }
}