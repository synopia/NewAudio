using System.Threading;
using NewAudio.Core;
using NewAudio.Nodes;
using NUnit.Framework;
using Serilog;

namespace NewAudioTest
{
    [TestFixture]
    public class DevicesTest
    {
        [Test]
        public void TestLifecycle()
        {
            AudioService.Instance.Reset();
            AudioService.Instance.Init();

            InputDevice input = new InputDevice();
            OutputDevice output = new OutputDevice();
            
            output.UpdateInput(input.Output);

            AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(100));
            AudioService.Instance.Flow.PostLifecycleMessage(LifecyclePhase.Uninitialized, LifecyclePhase.Booting);
            Thread.Sleep(10);
            Log.Logger.Information("PHASE {i}->{phase} ", input.Phase, output.Phase);
            Assert.AreEqual(LifecyclePhase.Booting, input.Phase);
            Assert.AreEqual(LifecyclePhase.Booting, output.Phase);
        }
    }
}