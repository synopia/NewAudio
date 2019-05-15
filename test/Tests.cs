using NUnit.Framework;
using VL.NewAudio;

namespace NewAudioTest
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void Test1()
        {
            var x = new CircularSampleBuffer(4000);
            Assert.AreEqual(4000, x.Count);
        }
    }
}