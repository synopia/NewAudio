using System.Threading;
using NUnit.Framework;
using VL.NewAudio.Dispatcher;

namespace VL.NewAudioTest.Dispatcher
{
    [TestFixture]
    public class TestMessages
    {
        public static int CallbackCalled = 0;
        private NewAudio.Dispatcher.Dispatcher _dispatcher;

        public class AsyncTestMessage : Message
        {
            public override void MessageCallback()
            {
                CallbackCalled++;
            }
        }
        public class TestMessage : Message
        {
        }

        public class Sender : MessageListener
        {
            public int SenderCalled = 0;
            public override void HandleMessage(Message message)
            {
                SenderCalled++;
            }
        }

        public class TestAsync : AsyncUpdater
        {
            public int HandleAsyncCalled = 0;
            
            public override void HandleAsyncUpdate()
            {
                Thread.Sleep(20);
                HandleAsyncCalled ++;
            }
        }

        
        [SetUp]
        public void InitDispatcher()
        {
            _dispatcher = new NewAudio.Dispatcher.Dispatcher();
        }

        [TearDown]
        public void ShutdownDispatcher()
        {
            _dispatcher.Dispose();
        }

        [Test]
        public void TestSimple()
        {
            Assert.IsTrue(_dispatcher.IsRunning);
            new AsyncTestMessage().Post();
            Thread.Sleep(100);
            Assert.AreEqual(1, CallbackCalled);
        }
        [Test]
        public void Test2(){
            Assert.IsTrue(_dispatcher.IsRunning);
            var sender = new Sender();
            sender.PostMessage(new TestMessage());
            Thread.Sleep(100);
            Assert.AreEqual(1, sender.SenderCalled);
            sender.PostMessage(new TestMessage());
            sender.PostMessage(new TestMessage());
            sender.PostMessage(new TestMessage());
            Thread.Sleep(100);
            Assert.AreEqual(4, sender.SenderCalled);
        }

        [Test]
        public void TestEvent()
        {
            var a = new TestAsync();
            a.TriggerAsyncUpdate();
            Thread.Sleep(100);
            Assert.AreEqual(1, a.HandleAsyncCalled);
            a.TriggerAsyncUpdate();
            a.TriggerAsyncUpdate();
            a.TriggerAsyncUpdate();
            Thread.Sleep(100);
            Assert.AreEqual(2, a.HandleAsyncCalled);
        }
        [Test]
        public void TestAsyncSupport()
        {
            var counter = 0;
            var a = new AsyncUpdateSupport(()=>counter++);
            a.TriggerAsyncUpdate();
            Thread.Sleep(100);
            Assert.AreEqual(1, counter);
            a.TriggerAsyncUpdate();
            a.TriggerAsyncUpdate();
            a.TriggerAsyncUpdate();
            Thread.Sleep(100);
            Assert.AreEqual(2, counter);
        }
    }
}