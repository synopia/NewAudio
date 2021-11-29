using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewAudio.Processor;
using NewAudio.Core;
using NewAudio.Devices;
using NUnit.Framework;
using Xt;

namespace NewAudioTest
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class PlayTest: BaseTest
    {
        protected override IXtPlatform CreatePlatform()
        {
            return new TestPlatform(new List<TestDevice>());
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
                using var o = new OutputDeviceProcessor(new OutputDeviceSelection("Wasapi: VoiceMeeter Input (VB-Audio VoiceMeeter VAIO) (Exclusive)"), new AudioProcessorConfig(){Channels = 2});
                o.Graph.AddOutput(o);
                // o.Device.UpdateFormat(new DeviceFormat(){SampleRate = 44100, BufferSizeMs = 100});
                Logger.Information("{D}", o);

                var sine = new SineGenProcessor(new AudioProcessorConfig(){AutoEnable = true});
                var noise = new NoiseGenProcessor(new AudioProcessorConfig(){AutoEnable = true});
                var gain = new MultiplyProcessor(new AudioProcessorConfig() { AutoEnable = true });
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
        }
    }
}