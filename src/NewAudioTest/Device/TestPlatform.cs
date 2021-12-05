using System;
using System.Linq;
using System.Threading;
using NewAudio.Device;
using NewAudio.Dsp;
using NewAudioTest.Processor;
using NUnit.Framework;
using NewAudio.Device;
using Xt;

namespace NewAudioTest.Device
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class TestPlatform
    {
        public class AU : IAudioStreamCallback
        {
            public int OnBuffer(XtStream stream, in XtBuffer buffer, object user)
            {
                Console.WriteLine("OnBuffer");
                return 0;
            }

            public void OnXRun(XtStream stream, int index, object user)
            {
                Console.WriteLine("OnX");
                
            }

            public void OnRunning(XtStream stream, bool running, ulong error, object user)
            {
                Console.WriteLine("OnRunning");
                
            }
        }
        [Test]
        public void Test1()
        {


            XtAudio.SetOnError((e) =>
            {
                Console.WriteLine(e);
            });
            
            var xtPlatform = XtAudio.Init("1", IntPtr.Zero);
            using var p = new XtAudioControl(new XtAudioService(xtPlatform));
            var loopback = "{0.0.0.00000000}.{36de8e18-5f41-4c04-8ebe-cc638b9e1db2}.{4}";
            var asio4all = "{232685C6-6548-49D8-846D-4141A3EF7560}";
            // p.Open(loopback, asio4all, 2, 2);

            using var s = new XtAudioService(xtPlatform);
            s.ScanForDevices();
            var dLoopback = s.OpenDevice(loopback);
            var dAsio4All = s.OpenDevice(asio4all);
            IAudioStreamCallback cb = new AU();
            // using var session = p.Open(dLoopback, dAsio4All, AudioChannels.Stereo, AudioChannels.Stereo, 0,0);
            // using var st1 = new AudioStream(dLoopback.Device, 2, true, false, AudioChannels.Stereo, 48000, XtSample.Int32, 100, cb);
            // using var st2 = new AudioStream(dAsio4All.Device, 2, false, true, AudioChannels.Stereo, 48000, XtSample.Int32, 100, cb);
            // st1.CreateStream();
            // st2.CreateStream();
            // st1.Start();
            // st2.Start();

            // using var xtPlatform = XtAudio.Init("1", IntPtr.Zero);
            // using var s = new XtAudioService(xtPlatform);
            // s.ScanForDevices();
            // var selection = s.GetDefaultDevice(true);
            // var selection = s.GetDevices()
            // .First(d => d.DeviceId == "{0.0.0.00000000}.{36de8e18-5f41-4c04-8ebe-cc638b9e1db2}.{3}");
            // Console.WriteLine(selection);
            // using var dev = s.CreateDevice(selection.DeviceId, selection.DeviceId);
            // dev.Open(AudioChannels.Stereo, AudioChannels.Stereo, 0, 30);

            // using var player = new AudioProcessorPlayer();
            // player.SetProcessor(new AudioGraphTest.TestProc());
            // p.AddAudioCallback(player);

            Thread.Sleep(10000);
        }
    }
}