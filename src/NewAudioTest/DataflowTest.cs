using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio;
using NewAudio.Blocks;
using NewAudio.Core;
using NUnit.Framework;
using Serilog;
using SharedMemory;
using VL.NewAudio.Core;
using AudioFormat = NewAudio.Core.AudioFormat;

namespace NewAudioTest
{
    
    [TestFixture]
    public class DataflowTest
    {


        [Test]
        public void TestLifecycle()
        {
            AudioService.Instance.Init();
            var flow = new AudioDataflow(AudioService.Instance.Logger);
            var token = new PlayPauseStop();
            var format = new AudioFormat(48000, 256, 2, true);
            var b1 = new CircularBuffer("Test1", 64, format.BufferSize*4);
            var b2 = new CircularBuffer("Test2", 32, format.BufferSize*4);
            var input = new AudioInputBlock(b1, flow, format, token);
            var output = new AudioOutputBlock(b2, flow, format, token);
            input.LinkTo(output);
            output.LinkTo(input);
            Assert.AreEqual(LifecyclePhase.Uninitialized, input.CurrentPhase);
            Assert.AreEqual(LifecyclePhase.Uninitialized, output.CurrentPhase);

            input.Post(new LifecycleMessage(LifecyclePhase.Uninitialized, LifecyclePhase.Playing));

            Thread.Sleep(10);

            Assert.AreEqual(LifecyclePhase.Playing, input.CurrentPhase);
            Assert.AreEqual(LifecyclePhase.Playing, output.CurrentPhase);
            b1.Dispose();
            b2.Dispose();
        }
        // [Test]
        public void TestBufferAligned() {
            AudioService.Instance.Init();
            var token = new PlayPauseStop();

            var flow = new AudioDataflow(AudioService.Instance.Logger);
            var format = new AudioFormat(48000, 8, 2, true);
            var b1 = new CircularBuffer("Test1", 16, format.BufferSize*4);
            var b2 = new CircularBuffer("Test2", 16, format.BufferSize*4);
            var input = new AudioInputBlock(b1, flow, format,token );
            var output = new AudioOutputBlock(b2, flow, format,token );
            input.LinkTo(output);
            output.LinkTo(input);
            input.Post(new LifecycleMessage(LifecyclePhase.Uninitialized, LifecyclePhase.Playing));
            Thread.Sleep(1);

            float[] buf = new float[64];
            for (int i = 0; i < 64; i++) buf[i] = i;
            Buffers.WriteAll(b1, buf, 64, token.GetToken());
            input.Post(new AudioDataRequestMessage( 64));
            Thread.Sleep(1);

            float[] outputBuffer = new float[64];
            Buffers.ReadAll(b2,outputBuffer, 64, token.GetToken());

            for (int i = 0; i < 64; i++)
            {
                Assert.AreEqual(i, outputBuffer[i]);
            }
            b1.Dispose();
            b2.Dispose();
        }
        // [Test]
        public void TestBufferUnaligned()
        {
            int messageSize = 77;
            AudioService.Instance.Init();
            var token = new PlayPauseStop();

            var flow = new AudioDataflow(AudioService.Instance.Logger);
            var format = new AudioFormat(48000, 8, 2, true);
            var b1 = new CircularBuffer("Test1", 64, format.BufferSize*4);
            var b2 = new CircularBuffer("Test2", 32, format.BufferSize*4);
            var input = new AudioInputBlock(b1, flow, format, token);
            var output = new AudioOutputBlock(b2, flow, format, token);
            input.LinkTo(output, new DataflowLinkOptions(){PropagateCompletion = true});
            output.LinkTo(input);
            input.Post(new LifecycleMessage(LifecyclePhase.Uninitialized, LifecyclePhase.Playing));
            Thread.Sleep(1);
            
            
            var recycled = new AudioDataMessage(format, messageSize);
            float[] buf = new float[messageSize];
            for (int i = 0; i < messageSize; i++) buf[i] = i;
            Buffers.WriteAll(b1, buf, messageSize, token.GetToken());

            input.Post(new AudioDataRequestMessage(recycled, messageSize));
            Thread.Sleep(1);

            float[] outputBuffer = new float[messageSize];
            Buffers.ReadAll(b2, outputBuffer, messageSize,token.GetToken());

            for (int i = 0; i < messageSize; i++)
            {
                Assert.AreEqual(i, outputBuffer[i]);
            }
            b1.Dispose();
            b2.Dispose();
        }
    }
}