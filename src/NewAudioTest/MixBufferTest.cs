using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Internal;
using NewAudio.Nodes;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace NewAudioTest
{
    [TestFixture]
    public class MixBufferTest: BaseDeviceTest
    {
        [Test]
        public void TestSimple2ChInterleaved()
        {
            var outputFormat = new AudioFormat(48000, 512, 2, true);
            var signal = BuildSignal(outputFormat);
            var buf = new ByteArrayMixBuffer("1", outputFormat);
            buf.WriteChannelsInterleaved(0, 2, signal);

            float[] b = buf.GetFloatArray();
            AssertSignal(signal, b);
        }

        [Test]
        public void TestSimple2x1ChTo2ChInterleaved()
        {
            var outputFormat = new AudioFormat(48000, 512, 2, true);
            var signal = BuildSignal(outputFormat);
            var left = BuildSignal(outputFormat.WithChannels(1), 0);
            var right = BuildSignal(outputFormat.WithChannels(1), 1);
            var buf = new ByteArrayMixBuffer("1", outputFormat);
            buf.WriteChannel(0, left);
            buf.WriteChannel(1, right);

            float[] b = buf.GetFloatArray();
            AssertSignal(signal, b);
        }

        [Test]
        public void TestMixBuffers()
        {
            var format = new AudioFormat(48000, 512, 2);
            var mix = new MixBuffers(2, 4, format);
            var rounds = 10;
            var played = new float[rounds][];
            var t1 = Task.Run(() =>
            {
                var count = 0;
                while (count<rounds)
                {
                    var left = BuildSignal(format.WithChannels(1), count*2);
                    var buf = mix.GetMixBuffer(0);
                    buf.WriteChannel(0, left);
                    mix.ReturnMixBuffer(0);
                    count++;
                }
            });
            var t2 = Task.Run(() =>
            {
                var count = 0;
                while (count<rounds)
                {
                    var right = BuildSignal(format.WithChannels(1), count*2+1);
                    var buf = mix.GetMixBuffer(1);
                    Task.Delay(1).Wait();
                    buf.WriteChannel(1, right);
                    mix.ReturnMixBuffer(1);
                    count++;
                }
            });

            var t3 = Task.Run(() =>
            {
                var count = 0;
                while (count<rounds)
                {
                    played[count] = mix.GetPlayBuffer().GetFloatArray();
                    mix.DonePlaying();
                    count++;
                }
            });

            Task.WaitAll(new[] { t1, t2, t3 });
            for (int i = 0; i < rounds; i++)
            {
                var signal = BuildSignal(format, i*2);
                AssertSignal(signal, played[i]);
            }
            /*
            var DeviceParams1 = AudioParams.Create<DeviceParams>();
            var DeviceParams2 = AudioParams.Create<DeviceParams>();

            DeviceParams1.SamplingFrequency.Value = SamplingFrequency.Hz16000;
            DeviceParams2.SamplingFrequency.Value = SamplingFrequency.Hz16000;
            DeviceParams1.Channels.Value = 1;
            DeviceParams1.ChannelOffset.Value = 0;
            DeviceParams2.Channels.Value = 1;
            DeviceParams2.ChannelOffset.Value = 1;
            
            var output1 = DriverManager.Resource.GetOutputDevice(OutputDevice);
            var output2 = DriverManager.Resource.GetOutputDevice(OutputDevice);
            output1.Bind(DeviceParams1);
            output2.Bind(DeviceParams2);
            
            output1.Device.Update();
        */
        }
    }
}