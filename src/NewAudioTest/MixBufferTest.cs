using System;
using System.Threading;
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
        public void TestSimple2x1ChTo2ChInterleaved2()
        {
            var outputFormat = new AudioFormat(48000, 512, 2, true);
            var signal = BuildSignal(outputFormat);
            var left = BuildSignal(outputFormat.WithChannels(1), 0);
            var right = BuildSignal(outputFormat.WithChannels(1), 1);
            var buf = new ByteArrayMixBuffer("1", outputFormat);
            buf.WriteChannelsInterleaved(0, 1, left);
            buf.WriteChannelsInterleaved(1, 1, right);

            float[] b = buf.GetFloatArray();
            AssertSignal(signal, b);
        }

        [Test]
        public void TestMixBuffers()
        {
            var format = new AudioFormat(48000, 512, 2);
            var mix = new MixBuffers(2, 4, format);
            var rounds = 100;
            var played = new float[rounds][];
            var random = new Random();
            var token = new CancellationTokenSource().Token;
            var t1 = Task.Run(() =>
            {
                var count = 0;
                while (count<rounds)
                {
                    var left = BuildSignal(format.WithChannels(1), count*2);
                    var buf = mix.GetWriteBuffer(token);
                    if (buf != null)
                    {
                        buf.WriteChannelsInterleaved(0, 1, left);
                    }

                    Task.Delay(random.Next(25)).Wait(token);
                    count++;
                }
            });
            var t2 = Task.Run(() =>
            {
                var count = 0;
                while (count<rounds)
                {
                    var right = BuildSignal(format.WithChannels(1), count*2+1);
                    var buf = mix.GetWriteBuffer(token);
                    if (buf != null)
                    {
                        buf.WriteChannelsInterleaved(1, 1, right);
                    }
                    Task.Delay(random.Next(30)).Wait();

                    count++;
                }
            });

            var t3 = Task.Run(() =>
            {
                var count = 0;
                while (count<rounds)
                {
                    var readBuffer = mix.GetReadBuffer(token);
                    if (readBuffer != null)
                    {
                        played[count] = readBuffer.GetFloatArray();
                        count++;
                    }
                }
            });

            Task.WaitAll(new[] { t1, t2, t3 });
            for (int i = 0; i < rounds-1; i++)
            {
                var signal = BuildSignal(format, i*2);
                AssertSignal(signal, played[i+1]);
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