using System.Threading;
using NewAudio.Core;
using NewAudio.Nodes;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class AudioServiceTest
    {
        [Test]
        public void TestDispose()
        {
            AudioService.Instance.Reset();

            var input = new InputDevice();
            var output = new OutputDevice();
            
            output.Connect.Invoke(input.Output);
            Thread.Sleep(1);
            AudioService.Instance.Dispose();
            Thread.Sleep(1);
        }
    }
}