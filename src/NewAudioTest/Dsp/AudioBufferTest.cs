using System;
using VL.NewAudio.Dsp;
using NUnit.Framework;

namespace VL.NewAudioTest.Dsp
{
    [TestFixture]
    public class AudioBufferTest
    {
        [Test]
        public void TestCopy()
        {
            using var b = new AudioBuffer(2, 1024);
            int count = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < 1024; i++)
                {
                    b[ch, i] = count++;
                }
            }

            using var c = new AudioBuffer(b.GetWriteChannels(/*1*/), 1, 1024);
            int j = 0;
            count = 1024;
            for (int i = 0; i < 1024; i++)
            {
                c[j, i] = count++;
            }
            count = 1;
            for (int i = 0; i < 1024; i++)
            {
                c[j, i] = count;
            }
            for (int ch = 0; ch < 2; ch++)
            {
                count = 1;
                for (int i = 0; i < 1024; i++)
                {
                    b[ch, i] = count++;
                }
            }
        }

        [Test]
        public void TestGetSet()
        {
            using var b = new AudioBuffer(2, 1024);
            Assert.AreEqual(2*1024, b.Size);
            int count = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < 1024; i++)
                {
                    b[ch, i] = count++;
                }
            }
            using var c = new AudioBuffer(b);
            count = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < 1024; i++)
                {
                    Assert.AreEqual((float)count, b[ch,i]);
                    Assert.AreEqual((float)count++, c[ch,i]);
                }
            }
        }

        private (AudioBuffer, AudioBuffer) FillBuffers(int inputChannel, int outputChannel, int frames)
        {
            AudioBuffer input = new AudioBuffer(inputChannel, frames);
            AudioBuffer output = new AudioBuffer(outputChannel, frames);
            
            int count = 1;
            for (int ch = 0; ch < inputChannel; ch++)
            {
                for (int i = 0; i < frames; i++)
                {
                    input[ch, i] = count++;
                }
            }

            count = 1;
            for (int ch = 0; ch < outputChannel; ch++)
            {
                for (int i = 0; i < frames; i++)
                {
                    output[ch, i] = count++;
                }
            }

            return (input, output);
        }

        [Test]
        public void TestMergeOutputGtInput()
        {
            var (input, output) = FillBuffers(2, 5, 512);

            var target = new AudioBuffer(5, 512);
            target.Merge(input, output, 2, 5);

            int count = 1;
            // inputs should stay 
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < 512; i++)
                {
                    Assert.AreEqual(count++, target[ch, i]);
                }
            }
            // output should zeroed
            for (int ch = 2; ch < 5; ch++)
            {
                for (int i = 0; i < 512; i++)
                {
                    Assert.AreEqual(0, target[ch, i]);
                }
            }
        }
        
        [Test]
        public void TestMergeInputGtOutput()
        {
            var (input, output) = FillBuffers(5, 2, 512);

            var target = new AudioBuffer(5, 512);
            target.Merge(input, output, 5, 2);

            int count = 1;
            // inputs should stay 
            for (int ch = 0; ch < 5; ch++)
            {
                for (int i = 0; i < 512; i++)
                {
                    Assert.AreEqual(count++, target[ch, i]);
                }
            }
        }

        [Test]
        public void TestCombining()
        {
            var numChannels = 4;
            var numFrames = 512;
            
            var b1 = new AudioBuffer(2, numFrames);
            var b2 = new AudioBuffer(2, numFrames);
            b1.Zero();
            b2.Zero();
            Memory<float>[] channels = new Memory<float>[4];
            channels[0] = b1.GetWriteChannel(0);
            channels[1] = b1.GetWriteChannel(1);
            channels[2] = b2.GetWriteChannel(0);
            channels[3] = b2.GetWriteChannel(1);

            var t = new AudioBuffer(channels, 4, 512);
            
            int count = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < numFrames; i++)
                {
                    b1[ch, i] = count++;
                    b2[ch, i] = count*10;
                }
            }
            
            count = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < numFrames; i++)
                {
                    Assert.AreEqual(count++, t[ch, i]);
                    Assert.AreEqual(count*10, t[ch+2, i]);
                }
            }
        }
    }
}