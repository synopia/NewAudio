using System;
using System.Linq;
using System.Threading;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using NUnit.Framework;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.NewAudio;
using Xt;

namespace NewAudioTest
{
    public static class CallCounter
    {
        public static int TestDeviceCreated = 0;
        public static int TestDeviceDisposed = 0;
    }

    public class TestPlatform : IXtPlatform
    {
        public void Dispose()
        {
        }

        public IXtService GetService(XtSystem system)
        {
            return new TestService();
        }

        public Action<string> OnError { get; set; }
        public void DoOnError(string message)
        {
            throw new NotImplementedException();
        }
    }

    public class TestStream : IXtStream
    {
        public void Dispose()
        {
        }

        public XtStream GetXtStream()
        {
            throw new NotImplementedException();
        }

        public XtFormat GetFormat()
        {
            return new XtFormat(new XtMix(48000, XtSample.Float32), new XtChannels(2, 0, 2, 0));
        }

        public int GetFrames()
        {
            return 512;
        }

        public XtLatency GetLatency()
        {
            return new XtLatency();
        }

        private bool _running;

        public bool IsRunning()
        {
            return _running;
        }

        public void Start()
        {
            _running = true;
        }

        public void Stop()
        {
            _running = false;
        }
    }

    public class TestDevice : IXtDevice
    {
        public TestDevice()
        {
            CallCounter.TestDeviceCreated++;
        }

        public void Dispose()
        {
            CallCounter.TestDeviceDisposed++;
        }

        public XtBufferSize GetBufferSize(in XtFormat format)
        {
            return new XtBufferSize();
        }

        public int GetChannelCount(bool output)
        {
            return 2;
        }

        public XtMix? GetMix()
        {
            return new XtMix(48000, XtSample.Float32);
        }

        public IXtStream OpenStream(in XtDeviceStreamParams param, object user)
        {
            return new TestStream();
        }

        public bool SupportsAccess(bool interleaved)
        {
            return true;
        }

        public bool SupportsFormat(in XtFormat format)
        {
            return true;
        }

        public string GetChannelName(bool output, int index)
        {
            return "";
        }
    }

    public class TestService : IXtService
    {
        public IXtDevice OpenDevice(string id)
        {
            return new TestDevice();
        }

        public IXtDeviceList OpenDeviceList(XtEnumFlags flags)
        {
            return new TestDeviceList();
        }

        public string GetDefaultDeviceId(bool output)
        {
            return output ? "OUTPUT" : "INPUT";
        }
    }

    public class TestDeviceList : IXtDeviceList
    {
        public void Dispose()
        {
        }

        public int GetCount()
        {
            return 2;
        }

        public string GetId(int index)
        {
            return index == 0 ? "OUTPUT" : "INPUT";
        }

        public string GetName(string id)
        {
            return id;
        }

        public XtDeviceCaps GetCapabilities(string id)
        {
            return id == "OUTPUT" ? XtDeviceCaps.Output : XtDeviceCaps.Input;
        }
    }

    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class BlockTest : BaseTest
    {


        [SetUp]
        public void Clear()
        {
            CallCounter.TestDeviceCreated = 0;
            CallCounter.TestDeviceDisposed = 0;
        }


        [Test]
        public void TestOutputDeviceWithNewDevice()
        {
            var sr = new AudioParam<SamplingFrequency>(SamplingFrequency.Hz48000);
            var bs = new AudioParam<float>(40);
            var outputSelection =
                new OutputDeviceSelection(AudioService.GetOutputDevices().First().ToString());
            using (var output = DeviceManager.GetOutputDevice(outputSelection,new AudioBlockFormat(){Channels = 2}))
            {
                Graph.OutputBlock = output;

                var sine = new SineGenBlock(new AudioBlockFormat());
                sine.Connect(Graph.OutputBlock);
                DeviceManager.UpdateFormat(sr,bs);
                Assert.AreEqual(48000, output.OutputSampleRate);
                Assert.AreEqual(512, output.FramesPerBlock);
                var buf = output.RenderInputs();
                Assert.AreEqual(1024, buf.Size);
            }

            AudioService.Dispose();
            Assert.AreEqual(1, CallCounter.TestDeviceCreated);
            Assert.AreEqual(1, CallCounter.TestDeviceDisposed);
        }

        [Test]
        public void TestOutputDeviceCloseFailsafe()
        {
            var sr = new AudioParam<SamplingFrequency>(SamplingFrequency.Hz48000);
            var bs = new AudioParam<float>(40);

            using (var handle = Resources.GetDeviceManager())
            {
                var deviceManager = handle.Resource;
                var outputSelection =
                    new OutputDeviceSelection(AudioService.GetOutputDevices().First().ToString());
                var output = deviceManager.GetOutputDevice(outputSelection, new AudioBlockFormat(){Channels = 2});
                DeviceManager.UpdateFormat(sr,bs);
                Graph.OutputBlock = output;
                var sine = new SineGenBlock(new AudioBlockFormat());

                sine.Connect(Graph.OutputBlock);

                var buf = output.RenderInputs();
                Assert.AreEqual(1024, buf.Size);
                handle.Resource.Dispose();
            }
            AudioService.Dispose();
            Assert.AreEqual(1, CallCounter.TestDeviceCreated);
            Assert.AreEqual(1, CallCounter.TestDeviceDisposed);
        }


        [Test]
        public void TestSetOutputLast()
        {
            var sr = new AudioParam<SamplingFrequency>(SamplingFrequency.Hz48000);
            var bs = new AudioParam<float>(40);

            using (var deviceManager = Resources.GetDeviceManager().Resource)
            {
                var outputSelection =
                    new OutputDeviceSelection(AudioService.GetOutputDevices().First().ToString());
                using var output = deviceManager.GetOutputDevice(outputSelection, new AudioBlockFormat(){Channels = 2});
                DeviceManager.UpdateFormat(sr,bs);

                var sine = new SineGenBlock(new AudioBlockFormat());
                var gain = new MultiplyBlock(new AudioBlockFormat());
                sine.Connect(gain);
                gain.Connect(output);
                Graph.OutputBlock = output;
                Assert.AreEqual(512, sine.FramesPerBlock);
                var buf1 = output.RenderInputs();
                Assert.AreEqual(512, sine.FramesPerBlock);
                var buf = output.RenderInputs();
                Assert.AreEqual(1024, buf.Size);
            }
            AudioService.Dispose();

            Assert.AreEqual(1, CallCounter.TestDeviceCreated);
            Assert.AreEqual(1, CallCounter.TestDeviceDisposed);
        }
    }
}