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
            var deviceSelections = deviceManager.GetOutputDevices().ToArray();
            Logger.Information("{X}", deviceSelections);
            var d = deviceSelections[0];//.SeFirst();
            Logger.Information("{D}", d.Name);
            var o = deviceManager.GetOutputDevice(new OutputDeviceSelection(d.ToString()), new DeviceBlockFormat(){Channels = 2});
            o.Graph.OutputBlock = o;
            Logger.Information("{D}", o);

            var sine = new SineGenBlock(new AudioBlockFormat(){AutoEnable = true});
            var noise = new NoiseGenBlock(new AudioBlockFormat(){AutoEnable = true});
            var gain = new MultiplyBlock(new AudioBlockFormat() { AutoEnable = true });
            sine.Params.Freq.Value = 100;
            gain.Params.Value.Value = 0.10f;
            // sine.Enable();
            // gain.Enable();
            // noise.Connect(gain);
            sine.Connect(gain);
            gain.Connect(o);
            
       
            noise.Enable();
            o.Graph.Enable();
            
            Task.Delay(10000).Wait();
            // o.Graph.UninitializeAllNodes();
        }
    }
}