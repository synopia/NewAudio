using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class TaskTest
    {
        [Test]
        public void TestIt()
        {
            AudioService.Instance.Init();
            var log = AudioService.Instance.Logger;
            var input = new BufferBlock<int>();
            var sut = new BufferBlock<int>();
            var received = 0;
            var produced = 0;
            var output = new ActionBlock<int>(i =>
            {
                // log.Information("RECEIVED {i}", i);
                received++;
            });
            input.LinkTo(sut);
            sut.LinkTo(output);
            var source = new CancellationTokenSource();
            
            var producer = Task.Run(async () =>
            {
                while(!source.Token.IsCancellationRequested)
                // for (int i = 0; i < 10000; i++)
                {
                    input.Post(1);
                    produced++;
                    // await Task.Delay(1);
                }
            }, source.Token);
            Task.Delay(10).Wait();

            var endTask = sut.Completion.ContinueWith(x =>
            {
                log.Information("{t}, {c}, {p}", x, received, produced);
                return Task.FromResult(true);
            });
            sut.Complete();
            endTask.GetAwaiter().GetResult();
            // sut.Completion.GetAwaiter().GetResult();
            log.Information("Finished waiting cancel1");
            Task.Delay(1000).Wait();

            log.Information("{c} {p}", received, produced);
            var endTask2 = producer.ContinueWith((x) =>
            {
                log.Information("FINISHED producing {c} {p}", received, produced);
                return Task.FromResult(true);
            });
            source.Cancel();
            endTask2.GetAwaiter().GetResult();
            log.Information("Finished waiting cancel2");
            Task.Delay(1000).Wait();
        }
    }
}