using System;
using System.Threading;
using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using NUnit.Framework;
using Serilog;

namespace NewAudioTest
{
    using NewAudioTest;

    [TestFixture]
    public class AudioBufferOutTest : BaseDeviceTest
    {
        [Test]
        public void TestIt()
        {
            using var input = new InputDevice();
            using var buf = new AudioBufferOut();
            input.Update(InputDevice, SamplingFrequency.Hz48000, 0, 1);
            Wait(input);

            buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);
            Wait(buf);

            input.PlayParams.Playing.Value = true;
            buf.PlayParams.Playing.Value = true;
            input.Update(InputDevice, SamplingFrequency.Hz48000, 0, 1);
            Wait(input, buf);

            var spread = buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);
            Wait(buf);
            input.Device.OnDataReceived(new byte[512 * 4]);
            input.Device.OnDataReceived(new byte[512 * 4]);
            Task.Delay(100).Wait();
            spread = buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);

            Assert.IsEmpty(buf.ErrorMessages());
            Assert.AreEqual(LifecyclePhase.Play, buf.Phase);
            Assert.AreEqual(1024, spread.Count);
        }
    }
}