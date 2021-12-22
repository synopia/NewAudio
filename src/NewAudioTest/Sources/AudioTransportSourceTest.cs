using System;
using System.Threading;
using NUnit.Framework;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Files;
using VL.NewAudio.Sources;

namespace VL.NewAudioTest.Sources
{
    public class AudioTransportSourceTest
    {
        [Test]
        public void TestBufferedLooped()
        {
            var r = new Mp3FileReader();
            using var b = new AudioFileBufferedReader(r, 1<<18,1000D);
            b.Open("../../Assets/PHONKIN SNARE 1.mp3");
            var s = new AudioFileReaderSource(b);
            s.IsLooping = true;
            var t = new AudioTransportSource();
            t.PrepareToPlay(48000, 512);
            t.SetSource(s, 0, 44100);
             t.Start();
            var buf = new AudioBuffer(2, 512);
            var info = new AudioBufferToFill(buf, 0, 512);
            var cnt = 3 * s.TotalLength / 512+55;
            for (int i = 0; i < cnt; i++)
            {
                // Assert.AreEqual((i*512%s.TotalLength), s.NextReadPos);
                // Assert.AreEqual((i*512%s.TotalLength), t.NextReadPos);
                Console.WriteLine($"{i}: {t.NextReadPos} {s.NextReadPos}");
                t.FillNextBuffer(info);
                Thread.Sleep(10);
            }

            for (int ch = 0; ch < buf.NumberOfChannels; ch++)
            {
                for (int i = 0; i < buf.NumberOfFrames; i++)
                {
                    Assert.AreNotEqual(0.0f, buf[ch][i]);
                }
            }
        }

    }
}