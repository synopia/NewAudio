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
            buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);

            UpdateDevices();
            input.PlayParams.Phase.Value = LifecyclePhase.Play;
            buf.PlayParams.Phase.Value = LifecyclePhase.Play;
            input.Update(InputDevice, SamplingFrequency.Hz48000, 0, 1);

            UpdateDevices();
            var spread = buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);
            UpdateDevices();
            input.Device.OnDataReceived(new byte[512 * 4]);
            input.Device.OnDataReceived(new byte[512 * 4]);
            spread = buf.Update(input.Output, 1024, 1, AudioBufferOutType.SkipHalf);
            UpdateDevices();

            Assert.IsEmpty(buf.ErrorMessages());
            Assert.AreEqual(LifecyclePhase.Play, buf.Phase);
            Assert.AreEqual(1024, spread.Count);
        }
    }
}