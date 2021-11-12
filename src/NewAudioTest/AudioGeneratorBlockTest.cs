using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class AudioGeneratorBlockTest : BaseTest
    {
        public AudioGeneratorBlockTest()
        {
            InitLogger<AudioGeneratorBlockTest>();
        }

        [Test]
        public void TestIt()
        {
            var f = new AudioFormat(48000, 512, 2);
            var sw = new Stopwatch();
            long samples = 0;
            var sink = new ActionBlock<AudioDataMessage>(input =>
            {
                samples += input.SampleCount;
                // log.Information("{time}: {input}", sw.Elapsed.TotalSeconds, input.BufferSize);
            });
            using var b = new AudioGeneratorBlock();
            b.Create(sink, f);

            sw.Start();
            Task.Delay(1000).Wait();
            sw.Stop();
            Logger.Information("{s}", samples / sw.Elapsed.TotalSeconds);
        }
    }
}