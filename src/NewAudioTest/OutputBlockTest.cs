using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewAudio.Block;
using NewAudio.Devices;
using NUnit.Framework;
using Xt;

namespace NewAudioTest
{
    [TestFixture]
    public class OutputBlockTest : BaseBlockTest
    {
        private int _disposeCalled;
        private TestStream _stream;
        private int _streamsCreated;

        protected override IList<TestDevice> Devices()
        {
            XtFormat format = new XtFormat(new XtMix(44100, XtSample.Float32), new XtChannels(0, 0, 2, 0));
            XtFormat format2 = new XtFormat(new XtMix(44100, XtSample.Float32), new XtChannels(0, 0, 4, 0));
            _disposeCalled = 0;
            return new[]
            {
                new TestDevice(XtSystem.ASIO, "1", "Out", XtDeviceCaps.Output, true, new[] { format, format2 })
                {
                    Outputs = 4,
                    OnDispose = () => { _disposeCalled++; },
                    OnOpenStream = (p) =>
                    {
                        _stream = new TestStream(p);
                        _streamsCreated++;
                        return _stream;
                    }
                }
            }.ToList();
        }

        [SetUp]
        public new void Init()
        {
            _disposeCalled = 0;
            _streamsCreated = 0;
        }

        [Test]
        public void TestMultipleJoinStream()
        {
            var outputSelection =
                new OutputDeviceSelection(AudioService.GetDefaultOutputDevices().First().ToString());
            using var output = new OutputDeviceBlock(outputSelection, new AudioBlockFormat() { Channels = 2 });
            Graph.AddOutput(output);
            while (!output.IsInitialized)
            {
                Task.Delay(10).Wait();
            }

            output.Enable();
            while (AudioService.Sessions.Count(s => s.IsProcessing)==0)
            {
                Task.Delay(10).Wait();
            }

            using var output2 = new OutputDeviceBlock(outputSelection, new AudioBlockFormat() { Channels = 2 });
            Graph.AddOutput(output2);

            Assert.IsTrue(output2.SampleRate != 0);
            Assert.IsTrue(output2.FramesPerBlock != 0);
            Assert.IsTrue(output2.IsInitialized);
            Assert.IsTrue(output.IsInitialized);
            Assert.IsTrue(output.IsEnabled);
            Assert.IsFalse(output2.IsEnabled);

            Assert.AreEqual(2, AudioService.Sessions.Count);
            Assert.AreEqual(2, output.NumberOfChannels);
            Assert.AreEqual(2, output2.NumberOfChannels);
            Assert.AreEqual(2, AudioService.Sessions.Count);

            Assert.AreEqual(1, AudioService.Sessions.Count(s => s.IsProcessing));
            output2.Enable();
            while (_streamsCreated == 0)
            {
                Task.Delay(10).Wait();
            }

            Assert.AreEqual(1, _streamsCreated);
            Assert.IsTrue(output2.IsEnabled);
            Assert.IsTrue(_stream.IsRunning());

            while (AudioService.Sessions.Count(s => s.IsProcessing) == 1)
            {
                Task.Delay(10).Wait();
            }

            Assert.AreEqual(1, _streamsCreated);
            Assert.IsTrue(_stream.IsRunning());

            output.Disable();
            Assert.IsFalse(output.IsEnabled);

            while (AudioService.Sessions.Count(s => s.IsProcessing) == 2)
            {
                Task.Delay(10).Wait();
            }

            Assert.AreEqual(1, _streamsCreated);
            Assert.IsTrue(_stream.IsRunning());

            output2.Disable();
            Assert.IsFalse(output2.IsEnabled);

            while (_stream.IsRunning())
            {
                Task.Delay(10).Wait();
            }
            Assert.AreEqual(0, AudioService.Sessions.Count(s => s.IsProcessing));
        }

        [Test]
        public void TestMultipleNewStream()
        {
            var outputSelection =
                new OutputDeviceSelection(AudioService.GetDefaultOutputDevices().First().ToString());
            using var output = new OutputDeviceBlock(outputSelection, new AudioBlockFormat() { Channels = 2 });
            Graph.AddOutput(output);
            while (!output.IsInitialized)
            {
                Task.Delay(10).Wait();
            }

            output.Enable();
            while (_streamsCreated == 0 || !_stream.IsRunning())
            {
                Task.Delay(10).Wait();
            }

            Assert.IsTrue(_stream.IsRunning());

            using var output2 = new OutputDeviceBlock(outputSelection, new AudioBlockFormat() { Channels = 4 });
            Graph.AddOutput(output2);

            Assert.IsTrue(output2.SampleRate != 0);
            Assert.IsTrue(output2.FramesPerBlock != 0);
            Assert.IsTrue(output2.IsInitialized);
            Assert.IsTrue(output.IsInitialized);
            Assert.IsTrue(output.IsEnabled);
            Assert.IsFalse(output2.IsEnabled);

            Assert.AreEqual(2, AudioService.Sessions.Count);
            Assert.AreEqual(2, output.NumberOfChannels);
            Assert.AreEqual(4, output2.NumberOfChannels);
            Assert.AreEqual(2, AudioService.Sessions.Count);

            Assert.AreEqual(1, AudioService.Sessions.Count(s => s.IsProcessing));

            output2.Enable();
            while (AudioService.Sessions.Count(s => s.IsProcessing) == 1)
            {
                Task.Delay(10).Wait();
            }
            Assert.AreEqual(2, _streamsCreated);
            Assert.IsTrue(output.IsEnabled);
            Assert.IsTrue(output2.IsEnabled);
            Assert.IsTrue(_stream.IsRunning());
            Assert.AreEqual(2, output.Session.Channels.OutputChannels);
            Assert.AreEqual(4, output2.Session.Channels.OutputChannels);
            Assert.AreEqual(4, _stream.Param.format.channels.outputs);
        }

        [Test]
        public void TestInit()
        {
            var outputSelection =
                new OutputDeviceSelection(AudioService.GetDefaultOutputDevices().First().ToString());
            using var output = new OutputDeviceBlock(outputSelection, new AudioBlockFormat() { Channels = 2 });
            Graph.AddOutput(output);
            while (!output.IsInitialized)
            {
                Task.Delay(10).Wait();
            }

            Assert.IsTrue(output.SampleRate != 0);
            Assert.IsTrue(output.FramesPerBlock != 0);
            Assert.IsFalse(output.IsEnabled);
            Assert.IsNotNull(_stream);

            output.SetEnabled(true);
            Assert.IsTrue(output.IsEnabled);
            Assert.AreEqual(1, AudioService.Sessions.Count);
            while (!_stream.IsRunning())
            {
                Task.Delay(10).Wait();
            }

            Assert.IsTrue(_stream.IsRunning());
            Assert.IsTrue(AudioService.Sessions[0].IsInitialized);
            Assert.IsTrue(AudioService.Sessions[0].IsProcessing);
            output.Disable();
            Assert.IsFalse(output.IsEnabled);

            while (_stream.IsRunning())
            {
                Task.Delay(10).Wait();
            }

            Assert.IsFalse(AudioService.Sessions[0].IsProcessing);
        }

        [Test]
        public void TestDispose()
        {
            var outputSelection =
                new OutputDeviceSelection(AudioService.GetDefaultOutputDevices().First().ToString());
            using (var output = new OutputDeviceBlock(outputSelection, new AudioBlockFormat() { Channels = 2 }))
            {
                Graph.AddOutput(output);
                while (!output.IsInitialized)
                {
                    Task.Delay(10).Wait();
                }

                Assert.AreEqual(1, AudioService.Sessions.Count);
            }

            while (AudioService.Sessions.Count > 0)
            {
                Task.Delay(10).Wait();
            }

            Assert.AreEqual(1, _disposeCalled);
        }
    }
}