﻿
using System;
using System.Threading;
using NUnit.Framework;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Files;

namespace VL.NewAudioTest.Sources
{
    [TestFixture]
    public class Mp3Test
    {
        [Test]
        public void TestMp3()
        {
            var r = new Mp3FileReader();
            r.Open("../../Assets/PHONKIN SNARE 1.mp3");
            var s = new AudioFileReaderSource(r);
            s.PrepareToPlay(44100, 512);
             
            var buf = new AudioBuffer(2, 512);
            var info = new AudioBufferToFill(buf, 0, 512);
            var cnt = s.TotalLength / 512+5;
            for (int i = 0; i < cnt; i++)
            {
                Assert.AreEqual(i*512, s.NextReadPos);
                Console.WriteLine(i);
                s.FillNextBuffer(info);
            }

            for (int ch = 0; ch < buf.NumberOfChannels; ch++)
            {
                for (int i = 0; i < buf.NumberOfFrames; i++)
                {
                    Assert.AreEqual(0.0f, buf[ch][i]);
                }
            }
        } 
        [Test]
        public void TestMp3Looped()
        {
            var r = new Mp3FileReader();
            r.Open("../../Assets/PHONKIN SNARE 1.mp3");
            var s = new AudioFileReaderSource(r);
            s.IsLooping = true;
            s.PrepareToPlay(44100, 512);
           
            var buf = new AudioBuffer(2, 512);
            var info = new AudioBufferToFill(buf, 0, 512);
            var cnt = s.TotalLength / 512+5;
            for (int i = 0; i < cnt; i++)
            {
                Assert.AreEqual((i*512%s.TotalLength), s.NextReadPos);
                Console.WriteLine(i);
                s.FillNextBuffer(info);
            }

            for (int ch = 0; ch < buf.NumberOfChannels; ch++)
            {
                for (int i = 0; i < buf.NumberOfFrames; i++)
                {
                    Assert.AreNotEqual(0.0f, buf[ch][i]);
                }
            }
        }

        [Test]
        public void TestBuffered()
        {
            var r = new Mp3FileReader();
            var b = new AudioFileBufferedReader(r, 1<<18, 1000D);
            b.Open("../../Assets/PHONKIN SNARE 1.mp3");
            var s = new AudioFileReaderSource(b);
            s.PrepareToPlay(44100, 512);
             
            var buf = new AudioBuffer(2, 512);
            var info = new AudioBufferToFill(buf, 0, 512);
            var cnt = s.TotalLength / 512+5;
            for (int i = 0; i < cnt; i++)
            {
                Assert.AreEqual(i*512, s.NextReadPos);
                Console.WriteLine(i);
                s.FillNextBuffer(info);
            }

            for (int ch = 0; ch < buf.NumberOfChannels; ch++)
            {
                for (int i = 0; i < buf.NumberOfFrames; i++)
                {
                    Assert.AreEqual(0.0f, buf[ch][i]);
                }
            }
        }

        [Test]
        public void TestBufferedLooped()
        {
            var r = new Mp3FileReader();
            using var b = new AudioFileBufferedReader(r, 1<<18,1000D);
            b.Open("../../Assets/PHONKIN SNARE 1.mp3");
            var s = new AudioFileReaderSource(b);
            s.IsLooping = true;
            s.PrepareToPlay(44100, 512);
             
            var buf = new AudioBuffer(2, 512);
            var info = new AudioBufferToFill(buf, 0, 512);
            var cnt = 3 * s.TotalLength / 512+5;
            for (int i = 0; i < cnt; i++)
            {
                Assert.AreEqual((i*512%s.TotalLength), s.NextReadPos);
                Console.WriteLine(i);
                s.FillNextBuffer(info);
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