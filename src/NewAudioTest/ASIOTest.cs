using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class AsioTest: BaseTest
    {
        [Test]
        public void TestAsio()
        {
            InitLogger<AsioTest>();
            var deviceManager = Factory.GetDriverManager().Resource;
            var d = deviceManager.GetOutputDevices().First();
            Logger.Information("{D}", d.Name);
            var o = deviceManager.GetOutputDevice(new OutputDeviceSelection(d.ToString()), new AudioBlockFormat());
            o.Graph.OutputBlock = o;
            Logger.Information("{D}", o);

            var sine = new SineGenBlock(new AudioBlockFormat(){AutoEnable = true});
            var noise = new NoiseBlock(new AudioBlockFormat(){AutoEnable = true});
            var gain = new MultiplyBlock(new AudioBlockFormat() { AutoEnable = true });
            sine.Params.Freq.Value = 1000;
            gain.Params.Value.Value = 0.000000f;
            
            // noise.Connect(gain);
            sine.Connect(gain);
            gain.Connect(o);
            
            sine.Enable();
            gain.Enable();
            noise.Enable();
            o.Graph.Enable();
            
            Task.Delay(1000).Wait();
            o.Graph.UninitializeAllNodes();
        }
    }
}