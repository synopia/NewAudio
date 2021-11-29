using System;
using NewAudio.Dsp;
using NewAudio.Processor;
using NUnit.Framework;

namespace NewAudioTest.Processor
{
    [TestFixture]
    public class AudioGraphTest
    {
        public class TestProc : AudioProcessor
        {
            private float phase = 0;
            public override string Name => "Test";

            public override void PrepareToPlay(int sampleRate, int framesPerBlock)
            {
            }

            
            public override void Process(AudioBuffer buffer)
            {
                for (int i = 0; i < buffer.NumberOfFrames; i++)
                {
                    var sample = AudioMath.SinF(AudioMath.TwoPi * phase)*0.1f;
                    for (int ch = 0; ch < buffer.NumberOfChannels; ch++)
                    {
                        buffer[ch, i] = sample;
                    }
                    phase = AudioMath.Fract(phase + 1000f / SampleRate);
                }   
            }

            public override void ReleaseResources()
            {
            }
        }

        [Test]
        public void TestOrderNodes()
        {
            var g = new AudioGraph2();
            var n1 = g.AddNode(new TestProc());
            var n2 = g.AddNode(new TestProc());
            var n3 = g.AddNode(new TestProc());
            var n4 = g.AddNode(new TestProc());
            var n5 = g.AddNode(new TestProc());

            Assert.AreNotEqual(n1.NodeId, n2.NodeId);
            Assert.AreNotEqual(n2.NodeId, n3.NodeId);
            Assert.AreNotEqual(n3.NodeId, n4.NodeId);
            Assert.AreNotEqual(n5.NodeId, n1.NodeId);

            g.AddConnection(new AudioGraph2.Connection(
                new AudioGraph2.NodeAndChannel(n1.NodeId, 0),
                new AudioGraph2.NodeAndChannel(n2.NodeId, 0)));
            g.AddConnection(new AudioGraph2.Connection(
                new AudioGraph2.NodeAndChannel(n2.NodeId, 0),
                new AudioGraph2.NodeAndChannel(n3.NodeId, 0)));
            g.AddConnection(new AudioGraph2.Connection(
                new AudioGraph2.NodeAndChannel(n3.NodeId, 0),
                new AudioGraph2.NodeAndChannel(n4.NodeId, 0)));
            g.AddConnection(new AudioGraph2.Connection(
                new AudioGraph2.NodeAndChannel(n4.NodeId, 0),
                new AudioGraph2.NodeAndChannel(n5.NodeId, 0)));

            var orderedNodeList = RenderingBuilder.CreateOrderedNodeList(g);
            Assert.AreEqual(new AudioGraph2.Node[]{n1,n2,n3,n4,n5}, orderedNodeList.ToArray());

            var program = new RenderingProgram();
            var b = new RenderingBuilder(g, program);
            Assert.AreEqual(1, program.NumBuffersNeeded);
        }
    }
}