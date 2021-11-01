using System.Threading;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using NUnit.Framework;
using Serilog;
using Serilog.Formatting.Display;

namespace NewAudioTest
{
    [TestFixture]
    public class MessageFilterTest
    {
        [Test]
        public void TestIt()
        {
            var log = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .MinimumLevel.Verbose()
                .CreateLogger();
            log.Information("XX");
            var actionReceived = 0;
            var workerReceived = 0;
            var action = new ActionBlock<LifecycleMessage>(msg =>
            {
                actionReceived++;
                log.Information("IN ACTION BLOCK {msg}", msg);
            });
            var worker = new ActionBlock<IAudioMessage>(msg =>
            {
                workerReceived++;
                log.Information("IN WORKER BLOCK {msg}", msg);
            });
            var filter = new MessageFilterBlock<LifecycleMessage>();

            var source = new BufferBlock<IAudioMessage>();
            source.LinkTo(filter);
            filter.LinkTo(action);
            source.LinkTo(worker);

            source.Post(new AudioDataMessage());
            source.Post(new LifecycleMessage());
            source.Post(new AudioDataMessage());
            source.Post(new LifecycleMessage());
            
            Thread.Sleep(10);
            Assert.AreEqual(2, workerReceived);
            Assert.AreEqual(2, actionReceived);
        }
    }
}