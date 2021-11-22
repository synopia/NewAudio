using System.Linq;
using System.Threading.Tasks;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Devices.Wasapi;
using NUnit.Framework;
using VL.Lib.Adaptive;

namespace NewAudioTest
{
    [TestFixture]
    public class WasapiTest: BaseTest
    {
        [Test]
        public void TestWasapi()
        {
            InitLogger<WasapiTest>();
            
            var deviceManager = Factory.GetDeviceManager().Resource;
            var deviceSelections = deviceManager.GetOutputDevices().ToArray();
            
            Logger.Information("{Count}", deviceSelections.Length);
            var w = new WasapiDevice(null, "{0.0.0.00000000}.{db25dad1-51be-4189-8739-cd801691a94d}", "X");
            w.Initialize();
            w.EnableProcessing();

            Task.Delay(1000).Wait();
        }
    }
}