using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class AudioGeneratorBlockTest
    {
        [Test]
        public void TestIt()
        {
            AudioService.Instance.Init();
            var log = AudioService.Instance.Logger.ForContext<AudioGeneratorBlockTest>();
            var b = new AudioGeneratorBlock();
            var f = new AudioFormat(48000, 512, 2);
            b.Create(f);
            var sw = new Stopwatch();
            long samples = 0;
            var sink = new ActionBlock<AudioDataMessage>(input =>
            {
                samples += input.SampleCount;
                // log.Information("{time}: {input}", sw.Elapsed.TotalSeconds, input.BufferSize);
            });
            
            b.LinkTo(sink);

            sw.Start();
            Task.Delay(10000).Wait();
            sw.Stop();
            log.Information("{s}", samples/sw.Elapsed.TotalSeconds);
        }
    }
}