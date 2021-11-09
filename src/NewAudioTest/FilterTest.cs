using System;
using NewAudio.Core;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class FilterTest
    {
        [Test]
        public void Test1()
        {
            var log = AudioService.Instance.Logger;
            var Q = 2;
            var fc = 1000;
            var fs = 96000;
            var omega = 2 * Math.PI * fc / fs;
            var K = Math.Tan(omega / 2);
            var W = K * K;
            var alpha = 1 + K;
            var DE = 1 + K / Q + W;

            log.Information($"omega={omega}, K={K}, W={W}, alpha={alpha}, DE={DE}");
            var a0 = 1;
            var a1 = 2 * (W - 1) / DE;
            var a2 = (1 - K / Q + W) / DE;

            var b0 = W / DE;
            var b1 = 2 * W / DE;
            var b2 = W / DE;

            var bb0 = 1 / DE;
            var bb1 = -2 * W / DE;
            var bb2 = 1 / DE;
            
            log.Information($"a0={a0},a1={a1},a2={a2},b0={b0},b1={b1},b2={b2},");
            log.Information($"a0={a0},a1={a1},a2={a2},b0={bb0},b1={bb1},b2={bb2},");
        }
    }
}