using System;
using NewAudio.Dsp;
using NewAudio.Processor;
using NUnit.Framework;
using VL.NewAudio.Processor;

namespace NewAudioTest.Processor
{
    [TestFixture]
    public class AudioGraphTest
    {

        [Test]
        public void TestOrderNodes()
        {
            var g = new AudioGraph();
            g.SetChannels(2,2);
            var gen = g.AddNode(new SineGenProcessor());
            var proc = g.AddNode(new MultiplyProcessor());
            var output = g.AddNode(new AudioGraphIOProcessor(true));
            var monitor = g.AddNode(new MonitorProcessor());
            Assert.NotNull(gen);
            Assert.NotNull(output);
            Assert.NotNull(monitor);
            Assert.NotNull(proc);

            
            g.AddConnection(new AudioGraph.Connection(
                new AudioGraph.NodeAndChannel(gen.NodeId, 0),
                new AudioGraph.NodeAndChannel(proc.NodeId, 0)));
            g.AddConnection(new AudioGraph.Connection(
                new AudioGraph.NodeAndChannel(proc.NodeId, 0),
                new AudioGraph.NodeAndChannel(output.NodeId, 0)));
            g.AddConnection(new AudioGraph.Connection(
                new AudioGraph.NodeAndChannel(proc.NodeId, 0),
                new AudioGraph.NodeAndChannel(output.NodeId, 1)));
            // g.AddConnection(new AudioGraph.Connection(
                // new AudioGraph.NodeAndChannel(n1.NodeId, 1),
                // new AudioGraph.NodeAndChannel(output.NodeId, 1)));
            g.AddConnection(new AudioGraph.Connection(
                new AudioGraph.NodeAndChannel(proc.NodeId, 0),
                new AudioGraph.NodeAndChannel(monitor.NodeId, 0)));
            // g.AddConnection(new AudioGraph.Connection(
                // new AudioGraph.NodeAndChannel(gen.NodeId, 0),
                // new AudioGraph.NodeAndChannel(monitor.NodeId, 1)));

            var orderedNodeList = RenderingBuilder.CreateOrderedNodeList(g);
            Assert.AreEqual(new[]{gen,proc, output, monitor}, orderedNodeList.ToArray());

            var program = new RenderingProgram();
            var b = new RenderingBuilder(g, program);
            var code = program.ToCode;
            Console.WriteLine(code);
            Assert.AreEqual(2, program.NumBuffersNeeded);
        }
    }
}