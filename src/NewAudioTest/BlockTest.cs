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
    }

    public class TestStream : IXtStream
    {
        public void Dispose()
        {
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
        public void TestOutputDeviceClose()
        {
            InitLogger<BlockTest>();
            var platform = new TestPlatform();
            Resources.SetResources(platform);
            var graph = Resources.GetAudioGraph().Resource;
            var deviceManager = Resources.GetDeviceManager().Resource;
            var outputSelection =
                new OutputDeviceSelection(deviceManager.GetOutputDevices().First().ToString());
            using (var output = deviceManager.GetOutputDevice(outputSelection, new DeviceBlockFormat()))
            {
                graph.OutputBlock = output;
                var sine = new SineGenBlock(new AudioBlockFormat());
                sine.Connect(graph.OutputBlock);

                var buf = graph.OutputBlock.RenderInputs();
                Assert.AreEqual(1024, buf.Size);
            }

            Assert.AreEqual(1, CallCounter.TestDeviceCreated);
            Assert.AreEqual(1, CallCounter.TestDeviceDisposed);
        }

        [Test]
        public void TestOutputDeviceCloseFailsafe()
        {
            InitLogger<BlockTest>();
            var platform = new TestPlatform();
            Resources.SetResources(platform);
            var graph = Resources.GetAudioGraph().Resource;
            using (var handle = Resources.GetDeviceManager())
            {
                var deviceManager = handle.Resource;
                var outputSelection =
                    new OutputDeviceSelection(deviceManager.GetOutputDevices().First().ToString());
                var output = deviceManager.GetOutputDevice(outputSelection, new DeviceBlockFormat());
                graph.OutputBlock = output;
                var sine = new SineGenBlock(new AudioBlockFormat());

                sine.Connect(graph.OutputBlock);

                var buf = graph.OutputBlock.RenderInputs();
                Assert.AreEqual(1024, buf.Size);
                handle.Resource.Dispose();
            }
            
            Assert.AreEqual(1, CallCounter.TestDeviceCreated);
            Assert.AreEqual(1, CallCounter.TestDeviceDisposed);
        }


        [Test]
        public void TestSetOutputLast()
        {
            InitLogger<BlockTest>();
            var platform = new TestPlatform();
            Resources.SetResources(platform);
            var graph = Resources.GetAudioGraph().Resource;
            using (var deviceManager = Resources.GetDeviceManager().Resource)
            {
                var outputSelection =
                    new OutputDeviceSelection(deviceManager.GetOutputDevices().First().ToString());
                using var output = deviceManager.GetOutputDevice(outputSelection, new DeviceBlockFormat());
                var sine = new SineGenBlock(new AudioBlockFormat());
                var gain = new MultiplyBlock(new AudioBlockFormat());
                sine.Connect(gain);
                gain.Connect(output);
                graph.OutputBlock = output;
                Assert.AreEqual(512, sine.FramesPerBlock);
                var buf1 = graph.OutputBlock.RenderInputs();
                Assert.AreEqual(512, sine.FramesPerBlock);
                var buf = graph.OutputBlock.RenderInputs();
                Assert.AreEqual(1024, buf.Size);
            }

            Assert.AreEqual(1, CallCounter.TestDeviceCreated);
            Assert.AreEqual(1, CallCounter.TestDeviceDisposed);
        }
    }
}