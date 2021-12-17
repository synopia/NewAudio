
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
        public void TestIt()
        {
            var r = new Mp3FileReader();
            r.Open("../../Assets/acidflow.mp3");
            var s = new AudioFileReaderSource(r);
            s.PrepareToPlay(44100, 512);
             
            var buf = new AudioBuffer(2, 512);
            var info = new AudioSourceChannelInfo(buf, 0, 512);
            s.GetNextAudioBlock(info);
            s.GetNextAudioBlock(info);
            s.GetNextAudioBlock(info);
            s.GetNextAudioBlock(info);
        }
        [Test]
        public void TestBuffered()
        {
            var r = new Mp3FileReader();
            var b = new AudioFileBufferedReader(r, 1<<18);
            b.Open("../../Assets/acidflow.mp3");
            var s = new AudioFileReaderSource(b);
            s.PrepareToPlay(44100, 512);
             
            var buf = new AudioBuffer(2, 512);
            var info = new AudioSourceChannelInfo(buf, 0, 512);
            Thread.Sleep(100);
            s.GetNextAudioBlock(info);
            s.GetNextAudioBlock(info);
            s.GetNextAudioBlock(info);
            s.GetNextAudioBlock(info);
        }
    }
}