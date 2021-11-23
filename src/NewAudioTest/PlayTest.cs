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
    public class PlayTest: BaseTest
    {
        public PlayTest() : base(false)
        {
        }

        [Test]
        public void TestPlay()
        {

            try
            {
                var deviceSelections = AudioService.GetOutputDevices().ToArray();
                // Logger.Information("{X}", deviceSelections);
                var d = deviceSelections[0];//.SeFirst();
                Logger.Information("{D}", d.Name);
                // var o = DeviceManager.GetOutputDevice(new OutputDeviceSelection(d.ToString()), new AudioBlockFormat(){Channels = 2});
                var o = DeviceManager.GetOutputDevice(new OutputDeviceSelection("Wasapi: VoiceMeeter Input (VB-Audio VoiceMeeter VAIO) (Exclusive)"), new AudioBlockFormat(){Channels = 2});
                o.Graph.OutputBlock = o;
                o.Device.UpdateFormat(new DeviceFormat(){SampleRate = 44100, BufferSizeMs = 100});
                Logger.Information("{D}", o);

                var sine = new SineGenBlock(new AudioBlockFormat(){AutoEnable = true});
                var noise = new NoiseGenBlock(new AudioBlockFormat(){AutoEnable = true});
                var gain = new MultiplyBlock(new AudioBlockFormat() { AutoEnable = true });
                sine.Params.Freq.Value = 1000;
                gain.Params.Value.Value = 0.30f;
                // sine.Enable();
                // gain.Enable();
                // noise.Connect(gain);
                sine.Connect(gain);
                gain.Connect(o);
            
       
                noise.Enable();
                o.Graph.Enable();
            
                Task.Delay(10000).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                
            }
            
            Graph.Dispose();
            DeviceManager.Dispose();
            AudioService.Dispose();
        }
    }
}