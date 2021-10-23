using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Forms;
using NewAudio;
using NewAudio.Internal;
using NUnit.Framework;

namespace NewAudioTest
{
    public interface IProvider
    {
        int Read(float[] buffer, int offset, int count);
    }

    public class Producer : IProvider
    {
        private CircularSampleBuffer _circularSampleBuffer;

        public Producer(int size)
        {
            _circularSampleBuffer = new CircularSampleBuffer(size);

            Task.Run(async () =>
            {
                Console.WriteLine($"Start producing in thread {Thread.CurrentThread.GetHashCode()}");
                var random = new Random();
                while (true)
                {
                    float[] buf = new float[random.Next(10, 100)];
                    if (_circularSampleBuffer.FreeSpace > buf.Length)
                    {
                        var w = _circularSampleBuffer.Write(buf, 0, buf.Length);
                        Console.WriteLine($"Written {w} floats, free: {_circularSampleBuffer.FreeSpace}");
                        await Task.Delay(random.Next(50, 200));
                    }
                    else
                    {
                        await Task.Delay(1);
                    }
                }
            });
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return _circularSampleBuffer.Read(buffer, offset, count);
        }
    }

    class Consumer
    {
        private float[] _buffer;
        private IProvider _provider;

        public Consumer(int size, IProvider provider)
        {
            _provider = provider;
            _buffer = new float[size];

            Task.Run(async () =>
            {
                Console.WriteLine($"Start consuming in thread {Thread.CurrentThread.GetHashCode()}");
                var random = new Random();
                while (true)
                {
                    var pos = 0;
                    while (pos < _buffer.Length)
                    {
                        pos += provider.Read(_buffer, pos, _buffer.Length - pos);
                        await Task.Delay(1);
                    }

                    Console.WriteLine($"Read buffer");
                }
            });
        }
    }

    [TestFixture]
    public class AsyncTest
    {
        [Test]
        public void Run()
        {
            Console.WriteLine($"{Thread.CurrentThread.GetHashCode()}");
            var p = new Producer(2000);
            var c = new Consumer(100, p);
            Thread.Sleep(20000);
        }

        void Produce(int count, ITargetBlock<AudioBuffer> targetBlock)
        {
            var random = new Random();
            var value = 0;
            while (count > 0)
            {
                var buf = new AudioBuffer(null, 200);
                for (int i = 0; i < 200; i++)
                {
                    buf.Data[i] = value++;
                }

                targetBlock.Post(buf);
                count--;
            }

            targetBlock.Complete();
        }

        async Task<int> Consume(ISourceBlock<AudioBuffer> sourceBlock)
        {
            var total = 0;
            var value = 0;
            while (await sourceBlock.OutputAvailableAsync())
            {
                AudioBuffer buf = await sourceBlock.ReceiveAsync();
                Console.WriteLine($"l={buf.Count}");
                for (int i = 0; i < 170; i++)
                {
                    Assert.AreEqual(value++, buf.Data[i]);
                }
                total += 1;
            }

            return total;
        }

   

        [Test]
        public async Task Run2()
        {
            var producer = new BufferBlock<AudioBuffer>();
            var format = new AudioFormat(1, 44100, 512);
            var splitter = new AudioFlowBuffer(format, 2000, 64);
            var combiner = new AudioFlowBuffer(format, 2000, 170);

            producer.LinkTo(splitter);
            splitter.LinkTo(combiner);
            producer.Completion.ContinueWith(delegate { splitter.Complete(); });
            splitter.Completion.ContinueWith(delegate { combiner.Complete(); });
            var task = Consume(combiner);
                Produce(10, producer);
                var total = await task;
                Console.WriteLine(total);
        }
    }
}