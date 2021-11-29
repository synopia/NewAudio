using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFTW.NET;
using NewAudio.Processor;
using NewAudio.Devices;
using NUnit.Framework;
using Xt;

namespace NewAudioTest.Processor
{
    [TestFixture]
    public class RenderProcessorTest : BaseProcessorTest
    {
        private int _disposeCalled;
        private TestStream _stream;
        private int _streamsCreated;

        protected override IList<TestDevice> Devices()
        {
            XtFormat format = new XtFormat(new XtMix(44100, XtSample.Float32), new XtChannels(0, 0, 2, 0));
            XtFormat format2 = new XtFormat(new XtMix(44100, XtSample.Float32), new XtChannels(0, 0, 4, 0));
            XtFormat format4 = new XtFormat(new XtMix(44100, XtSample.Float32), new XtChannels(2, 0, 0, 0));
            XtFormat format3 = new XtFormat(new XtMix(44100, XtSample.Float32), new XtChannels(2, 0, 2, 0));
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
                    },
                    Interleaved = true
                },
                new TestDevice(XtSystem.ASIO, "2", "In", XtDeviceCaps.Input, true, new[] { format, format2 })
                {
                    Inputs = 4,
                    OnDispose = () => { _disposeCalled++; },
                    OnOpenStream = p =>
                    {
                        _stream = new TestStream(p);
                        _streamsCreated++;
                        return _stream;
                    },
                    Interleaved = true
                },
                new TestDevice(XtSystem.ASIO, "3", "InOut", XtDeviceCaps.Input | XtDeviceCaps.Output, false,
                    new[] { format3, format4, format2 })
                {
                    Inputs = 4,
                    Outputs = 4,
                    OnDispose = () => { _disposeCalled++; },
                    OnOpenStream = p =>
                    {
                        _stream = new TestStream(p);
                        _streamsCreated++;
                        return _stream;
                    },
                    Interleaved = true
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
        public void TestPlaybackOneNodeOneDeviceUpMix()
        {
            var outputSelection =
                new OutputDeviceSelection(AudioService.GetDefaultOutputDevices().First().ToString());
            using var output = new OutputDeviceProcessor(outputSelection, new AudioProcessorConfig() { Channels = 2 });
            Graph.AddOutput(output);
            var counter = 1;
            using var input = new InputDelegateProcessor((buffer, sr, frames) =>
            {
                for (int i = 0; i < frames; i++)
                {
                    buffer[i] = counter / (float)sr;
                    counter++;
                }
            }, new AudioProcessorConfig() { Channels = 1 });
            input.Connect(output);
            output.Enable();
            input.Enable();
            while (_stream == null || !_stream.IsRunning())
            {
                Task.Delay(10).Wait();
            }

            var counter2 = 1;
            foreach (var numFrames in new int[] { 512, 300, 212, 512 })
            {
                float[] outputBuffer = new float[output.NumberOfChannels * numFrames];
                using var pin = new PinnedArray<float>(outputBuffer);
                var buffer = new XtBuffer
                {
                    frames = numFrames,
                    output = pin.Pointer
                };
                AudioService.OnBuffer(buffer);

                for (int i = 0; i < numFrames; i++)
                {
                    Assert.AreEqual(counter2 / (float)output.SampleRate, outputBuffer[i * 2]);
                    Assert.AreEqual(counter2 / (float)output.SampleRate, outputBuffer[i * 2 + 1]);
                    counter2++;
                }
            }
        }

        [Test]
        public void TestPlaybackTwoNodeOneDeviceUpMix()
        {
            var outputSelection =
                new OutputDeviceSelection(AudioService.GetDefaultOutputDevices().First().ToString());
            using var output = new OutputDeviceProcessor(outputSelection, new AudioProcessorConfig() { Channels = 2 });
            using var output2 = new OutputDeviceProcessor(outputSelection, new AudioProcessorConfig() { Channels = 4 });
            Graph.AddOutput(output);
            Graph.AddOutput(output2);
            var counter = 1;
            using var input = new InputDelegateProcessor((buffer, sr, frames) =>
            {
                for (int i = 0; i < frames; i++)
                {
                    buffer[i] = counter / (float)sr;
                    counter++;
                }
            }, new AudioProcessorConfig() { Channels = 1 });
            input.Connect(output);
            input.Connect(output2);
            output.Enable();
            output2.Enable();
            input.Enable();
            while (AudioService.Sessions.Count(s => s.IsProcessing) != 2)
            {
                Task.Delay(10).Wait();
            }

            var counter2 = 1;
            foreach (var numFrames in new int[] { 512, 300, 212, 512 })
            {
                float[] outputBuffer = new float[output2.NumberOfChannels * numFrames];
                using var pin = new PinnedArray<float>(outputBuffer);
                var buffer = new XtBuffer
                {
                    frames = numFrames,
                    output = pin.Pointer
                };
                AudioService.OnBuffer(buffer);

                for (int i = 0; i < numFrames; i++)
                {
                    Assert.AreEqual(2 * counter2 / (float)output.SampleRate, outputBuffer[i * 4]);
                    Assert.AreEqual(2 * counter2 / (float)output.SampleRate, outputBuffer[i * 4 + 1]);
                    Assert.AreEqual(counter2 / (float)output.SampleRate, outputBuffer[i * 4 + 2]);
                    Assert.AreEqual(counter2 / (float)output.SampleRate, outputBuffer[i * 4 + 3]);
                    counter2++;
                }
            }
        }

        [Test]
        public void TestFullDuplex()
        {
            var outputSelection =
                new OutputDeviceSelection("ASIO: InOut");
            using var output = new OutputDeviceProcessor(outputSelection, new AudioProcessorConfig() { Channels = 2 });
            var inputSelection = new InputDeviceSelection("ASIO: InOut");
            using var input = new InputDeviceProcessor(inputSelection, new AudioProcessorConfig() { Channels = 2 });
            Graph.AddOutput(output);
            input.Connect(output);
            output.Enable();
            input.Enable();
            while (AudioService.Sessions.Count(s => s.IsProcessing) != 2)
            {
                Task.Delay(10).Wait();
            }
            int counter = 1;
            int counter2 = 1;
            foreach (var numFrames in new int[] { 512, 300, 212, 512 })
            {
                float[] inputBuffer = new float[output.NumberOfChannels * numFrames];
                for (int i = 0; i < numFrames; i++)
                {
                    inputBuffer[i * 2] = counter / (float)output.SampleRate;
                    inputBuffer[i * 2 + 1] = counter / (float)output.SampleRate;
                    counter++;
                }

                float[] outputBuffer = new float[output.NumberOfChannels * numFrames];
                using var pin = new PinnedArray<float>(inputBuffer);
                using var pon = new PinnedArray<float>(outputBuffer);
                var buffer = new XtBuffer
                {
                    frames = numFrames,
                    output = pon.Pointer,
                    input = pin.Pointer,
                };
                AudioService.OnBuffer(buffer);
                for (int i = 0; i < numFrames; i++)
                {
                    Assert.AreEqual(counter2 / (float)output.SampleRate, outputBuffer[i * 2]);
                    Assert.AreEqual(counter2 / (float)output.SampleRate, outputBuffer[i * 2 + 1]);
                    counter2++;
                }
            }
        }
    }
}