using System;
using System.Threading;
using NUnit.Framework;
using Xt;

namespace NewAudioTest
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
   public class Aggregate
    {
        // Normally don't do I/O in the callback.
        static void OnXRun(XtStream stream, int index, object user)
        => Console.WriteLine("XRun on device " + index + ".");
        static void OnError(string message)
            => Console.WriteLine(message);

        static void OnRunning(XtStream stream, bool running, ulong error, object user)
        {
            string evt = running ? "Started" : "Stopped";
            Console.WriteLine("Stream event: " + evt + ", new state: " + stream.IsRunning() + ".");
            if (error != 0) Console.WriteLine(XtAudio.GetErrorInfo(error).ToString());
        }

        static int OnBuffer(XtStream stream, in XtBuffer buffer, object user)
        {
            XtSafeBuffer safe = XtSafeBuffer.Get(stream);
            safe.Lock(buffer);
            XtFormat format = stream.GetFormat();
            XtAttributes attrs = XtAudio.GetSampleAttributes(format.mix.sample);
            int bytes = buffer.frames * stream.GetFormat().channels.inputs * attrs.size;
            Buffer.BlockCopy(safe.GetInput(), 0, safe.GetOutput(), 0, bytes);
            safe.Unlock(buffer);
            return 0;
        }

        [Test]
        public void TestMain()
        {
            XtAggregateStreamParams aggregateParams;
            XtMix mix = new XtMix(48000, XtSample.Int32);
            XtFormat inputFormat = new XtFormat(mix, new XtChannels(2, 0, 0, 0));
            XtFormat outputFormat = new XtFormat(mix, new XtChannels(0, 0, 2, 0));

            using XtPlatform platform = XtAudio.Init(null, IntPtr.Zero);
            XtAudio.SetOnError(OnError);
            XtService service = platform.GetService(XtSystem.WASAPI);
            if (service == null || (service.GetCapabilities() & XtServiceCaps.Aggregation) == 0) return;

            var loopback = "{0.0.0.00000000}.{36de8e18-5f41-4c04-8ebe-cc638b9e1db2}.{4}";
            string defaultInput = loopback; //service.GetDefaultDeviceId(false);
            if (defaultInput == null) return;
            using XtDevice input = service.OpenDevice(defaultInput);
            if (!input.SupportsFormat(inputFormat)) return;

            string defaultOutput = service.GetDefaultDeviceId(true);
            if (defaultOutput == null) return;
            using XtDevice output = service.OpenDevice(defaultOutput);
            if (!output.SupportsFormat(outputFormat)) return;

            var asio4all = "{232685C6-6548-49D8-846D-4141A3EF7560}";

          Console.WriteLine(defaultOutput);

            XtAggregateDeviceParams[] deviceParams = new XtAggregateDeviceParams[2];
            deviceParams[0] = new XtAggregateDeviceParams(input, in inputFormat.channels, 30.0);
            deviceParams[1] = new XtAggregateDeviceParams(output, in outputFormat.channels, 30.0);
            XtStreamParams streamParams = new XtStreamParams(true, OnBuffer, OnXRun, OnRunning);
            aggregateParams = new XtAggregateStreamParams(in streamParams, deviceParams, 2, mix, output);
            using XtStream stream = service.AggregateStream(in aggregateParams, null);
            using XtSafeBuffer safe = XtSafeBuffer.Register(stream, true);
            stream.Start();
            Thread.Sleep(2000);
            stream.Stop();
        }
    }

}