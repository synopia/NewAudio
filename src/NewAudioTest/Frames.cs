using System;
using System.Threading.Tasks;
using NewAudio.Core;
using NUnit.Framework;
using SharedMemory;

namespace NewAudioTest
{
    [TestFixture]
    public class Frames
    {
        [Test]
        public void TestFrames()
        {
            AudioService.Instance.Init();
            byte[] frame;
            int pos;
            int frameSize = 256;
            frame = new byte[frameSize];
            int total = 100000;
            var buf = new byte[total];
            for (int i = 0; i < total; i++)
            {
                buf[i] = (byte)(i % 255);
            }

            var Buffer = new CircularBuffer("A", 200, frameSize);
            var Buffer2 = new CircularBuffer("A");
            var Logger = AudioService.Instance.Logger;
            var t1 = Task.Run(() =>
            {
                var remain = total;
                while (remain > 0)
                {
                    var sourcePos = 0;
                    while (sourcePos + frameSize < total)
                    {
                        Array.Copy(buf, sourcePos, frame, 0, frameSize);
                        Buffer.Write(frame);
                        remain -= frameSize;
                        sourcePos += frameSize;
                    }

                    pos = total - sourcePos;
                    if (total - sourcePos > 0)
                    {
                        Array.Copy(buf, sourcePos, frame, 0, pos);
                    }
                }
            });
            byte[] read = new byte[frameSize];
            var count = 0;
            var t2 = Task.Run(() =>
            {
                var remain = total;
                while (remain > 0)
                {
                    var r = Buffer2.Read(read);
                    remain -= r;
                    Logger.Information("r={r}", r);
                    if (r == frameSize)
                    {
                        count++;
                    }
                }
            });

            Task.WaitAll(new[]
            {
                t1, t2
            });

            Logger.Information("{count}", count);
        }
    }
}