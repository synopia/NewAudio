using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NewAudio.Internal;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class PipesTest
    {
        [Test]
        public void TestRandom()
        {
            var sha1 = SHA1.Create();
            var sha2 = SHA1.Create();
            var hashLen = 20;
            var count = 10000;
            var random = new Random();
            var success = 0;
            var failures = 0;
            using var inPipe = new InPipe("Test", true, msg =>
            {
                var hash = sha1.ComputeHash(msg, 0, msg.Length - hashLen);
                if( hash.SequenceEqual(msg.Skip(msg.Length-hashLen)))
                {
                    success++;
                }
                else
                {
                    failures++;
                }
            });
            using var outPipe = new OutPipe("Test", false);
            for (int i = 0; i < count; i++)
            {
                var len = random.Next(60) + 30;
                var buf = new byte[len + hashLen];
                random.NextBytes(buf);
                var hash = sha2.ComputeHash(buf, 0, len);
                Array.Copy(hash, 0, buf, len, hashLen);
                outPipe.Write(buf);
            }
            Task.Delay(1000).Wait();

            Assert.AreEqual(0, failures);
            Assert.AreEqual(count, success);

        }
    }
}