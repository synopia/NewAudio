﻿using System.Diagnostics;
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
            
            var f = new AudioFormat(48000, 512, 2);
            var sw = new Stopwatch();
            long samples = 0;
            var sink = new ActionBlock<AudioDataMessage>(input =>
            {
                samples += input.SampleCount;
                // log.Information("{time}: {input}", sw.Elapsed.TotalSeconds, input.BufferSize);
            });
            var b = new AudioGeneratorBlock();
            b.Create(sink, f);

            sw.Start();
            Task.Delay(1000).Wait();
            sw.Stop();
            log.Information("{s}", samples/sw.Elapsed.TotalSeconds);
        }
    }
}