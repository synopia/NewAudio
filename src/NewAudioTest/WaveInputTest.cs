using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class WaveInputTest
    {
        private void AssertBuffer(float[] expected, float[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i]);
            }
        }
        
        private float[] CreateBufferFilled(int start=0)
        {
            float[] buf = new float[1024];
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = i+start;
            }

            return buf;
        }
        private float[] CreateBufferNull()
        {
            float[] buf = new float[1024];
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = 0;
            }

            return buf;
        }

        private void ReadStartUpPhase(OutputDevice output)
        {
            float[] buf = CreateBufferNull();
            Thread.Sleep(1);
            var read = output.SampleProvider.Read(buf, 0, 1024);
            Assert.AreEqual(1024, read);
            Thread.Sleep(1);
            read = output.SampleProvider.Read(buf, 0, 1024);
            Assert.AreEqual(1024, read);
            Thread.Sleep(1);
            read = output.SampleProvider.Read(buf, 0, 1024);
            Assert.AreEqual(1024, read);
            Thread.Sleep(1);

        }
        /*
        [Test] 
        public void TestChangeDevice()
        {
            AudioCore.Instance.Restart();
            LogFactory.Instance.MinLevel = LogLevel.Trace;            
            var input = new WaveInput();
            input.ChangeDevice(new WaveInputDevice("Audio Generator"));
            input.ChangeSettings();
            
            Assert.NotNull(input.Output);
            Assert.NotNull(input.Output.SourceBlock);

            var output = new WaveOutput();
            output.ChangeDevice(new WaveOutputDevice("NullAudio"));
            output.ChangeSettings(input.Output);

            Assert.NotNull(output.Device);
            Assert.NotNull(output.Device.SampleProvider);

            input.IsPlaying = true;
            output.IsPlaying = true;

            ReadStartUpPhase(output.Device);
            
            float[] bufNull = CreateBufferNull();
            float[] bufFilled = CreateBufferFilled();
            float[] actual = CreateBufferFilled();
            output.Device.SampleProvider.Read(actual, 0, 1024);
            AssertBuffer(bufNull, actual);

            // Change device, it generates filled buffer
            input.ChangeDevice(new WaveInputDevice("Audio Generator Test"));
            Thread.Sleep(1);
            
            // Still playing and connected
            Assert.IsTrue(input.IsPlaying);
            Assert.IsTrue(output.IsPlaying);
            Thread.Sleep(1);
            
            ReadStartUpPhase(output.Device);
            // Filled buffer
            output.Device.SampleProvider.Read(actual, 0, 1024);
            AssertBuffer(bufFilled, actual);
           
        }
        */
        [Test] 
        public void TestReconnect()
        {
            AudioCore.Instance.Restart();
            var input = new WaveInput();
            input.ChangeDevice(new WaveInputDevice("Audio Generator Test"));
            input.ChangeSettings();
            
            var output = new WaveOutput();
            output.ChangeDevice(new WaveOutputDevice("NullAudio"));
            output.ChangeSettings(input.Output);

            input.IsPlaying = true;
            output.IsPlaying = true;

            ReadStartUpPhase(output.Device);
            
            output.ChangeSettings(null);
            Thread.Sleep(1);
            output.ChangeSettings(input.Output);
            Thread.Sleep(1);
            
            // Still playing and connected
            Assert.IsTrue(input.IsPlaying);
            Assert.IsTrue(output.IsPlaying);
            Thread.Sleep(1);
            
            float[] buf = CreateBufferNull();
            output.Device.SampleProvider.Read(buf, 0, 1024);
            AssertBuffer(CreateBufferFilled(1024), buf);
            Assert.IsTrue(output.Device.UnderRuns==0);
            Assert.IsTrue(input.Device.Overflows==0);
        }
        [Test] 
        public void TestReconnectLonger()
        {
            AudioCore.Instance.Restart();
            var input = new WaveInput();
            input.ChangeDevice(new WaveInputDevice("Audio Generator Test"));
            input.ChangeSettings();
            
            var output = new WaveOutput();
            output.ChangeDevice(new WaveOutputDevice("NullAudio"));
            output.ChangeSettings(input.Output);

            input.IsPlaying = true;
            output.IsPlaying = true;

            ReadStartUpPhase(output.Device);
            
            output.ChangeSettings(null);
            Thread.Sleep(1);
            
            // Still playing and connected
            Assert.IsTrue(input.IsPlaying);
            Assert.IsTrue(output.IsPlaying);
            Thread.Sleep(1);
            
            float[] buf = CreateBufferNull();
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            
            output.ChangeSettings(input.Output);
            Thread.Sleep(1);

            output.Device.SampleProvider.Read(buf, 0, 1024);
            AssertBuffer(CreateBufferFilled(0), buf);
            Assert.IsTrue(output.Device.UnderRuns==0);
            Assert.IsTrue(input.Device.Overflows==0);
        }
        
        [Test] 
        public void TestDisconnect()
        {
            AudioCore.Instance.Restart();
            var input = new WaveInput();
            input.ChangeDevice(new WaveInputDevice("Audio Generator Test"));
            input.ChangeSettings();
            
            var output = new WaveOutput();
            output.ChangeDevice(new WaveOutputDevice("NullAudio"));
            output.ChangeSettings(input.Output);

            input.IsPlaying = true;
            output.IsPlaying = true;

            ReadStartUpPhase(output.Device);
            
            output.ChangeSettings(null);
            Thread.Sleep(1);
            
            // Still playing and connected
            Assert.IsTrue(input.IsPlaying);
            Assert.IsTrue(output.IsPlaying);
            Thread.Sleep(1);
            
            float[] buf = CreateBufferNull();
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            Assert.IsTrue(output.Device.UnderRuns==0);
            Assert.IsTrue(input.Device.Overflows==0);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            AssertBuffer(CreateBufferNull(), buf);
            Assert.IsTrue(output.Device.UnderRuns>0);

        }
        
        [Test] 
        public void TestMultipleSources()
        {
            AudioCore.Instance.Restart();
            var input1 = new WaveInput();
            input1.ChangeDevice(new WaveInputDevice("Audio Generator Test"));
            input1.ChangeSettings();
            var input2 = new WaveInput();
            input2.ChangeDevice(new WaveInputDevice("Audio Generator Test"));
            input2.ChangeSettings();
            
            var output = new WaveOutput();
            output.ChangeDevice(new WaveOutputDevice("NullAudio"));
            output.ChangeSettings(input1.Output);

            input1.IsPlaying = true;
            output.IsPlaying = true;
            input2.IsPlaying = true;

            ReadStartUpPhase(output.Device);
            ReadStartUpPhase(output.Device);
            
            Assert.AreSame(input1.Device, input2.Device);
            
            Assert.IsTrue(input1.IsPlaying);
            Assert.IsTrue(output.IsPlaying);
            Assert.IsTrue(input2.IsPlaying);
            Thread.Sleep(1);
            
            float[] buf = CreateBufferNull();
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            AssertBuffer(CreateBufferFilled(0), buf);
            Assert.IsTrue(output.Device.UnderRuns==0);
            Assert.IsTrue(input1.Device.Overflows==0);
            Assert.IsTrue(input2.Device.Overflows==0);
            
            output.ChangeSettings(input2.Output);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            output.Device.SampleProvider.Read(buf, 0, 1024);
            Thread.Sleep(1);
            AssertBuffer(CreateBufferFilled(0), buf);
            
            
            Assert.IsTrue(output.Device.UnderRuns==0);
            Assert.IsTrue(input1.Device.Overflows==0);
            Assert.IsTrue(input2.Device.Overflows==0);
        }

    }
}