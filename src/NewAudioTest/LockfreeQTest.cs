using NewAudio;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class LockfreeQTest
    {
        [Test]
        public void Test()
        {
            MSQueue<int> Q = new MSQueue<int>();
            Q.enqueue(1);
            int i=0;
            Q.deque(ref i);
            Assert.AreEqual(1,i);
            
        }
    }
}