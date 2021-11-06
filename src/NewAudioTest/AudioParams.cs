using System.Collections.Generic;
using System.Threading.Tasks;
using NewAudio.Core;
using NUnit.Framework;

namespace NewAudioTest
{
    [TestFixture]
    public class AudioParamsTest
    {
        private class T : AudioParams
        {
#pragma warning disable 649
            public AudioParam<int> Aint;
            public AudioParam<float> Afloat;
            public AudioParam<int[]> AintArray;
            public AudioParam<IList<float>> AfloatList;
#pragma warning restore 649
        }

        [Test]
        public void TestCombinedCallbacks()
        {
            var t = new T();
            
            t.Aint.OnChange += Callback;
            t.Afloat.OnChange += Callback;
            
            t.Update();
            Assert.AreEqual(0, _cnt);
            t.Aint.Value = 1;
            t.Update();
            Assert.AreEqual(1, _cnt);
            t.Aint.Value = 2;
            t.Afloat.Value = 2;
            t.Update();
            Assert.AreEqual(2, _cnt);
        }
        private int _cnt = 0;
        private Task Callback()
        {
            _cnt++;
            return Task.CompletedTask;
        }
        [Test]
        public void TestCallbacks()
        {
            var t = new T();
            var aIntCnt = 0;
            var aIntArrCnt = 0;
            t.Aint.OnChange += () =>
            {
                aIntCnt++;
                return Task.CompletedTask;
            };
            t.AintArray.OnChange += () =>
            {
                aIntArrCnt++;
                return Task.CompletedTask;
            };
            t.Update();
            t.Aint.Value = 1;
            t.Update();
            t.Aint.Value = 1;
            t.Update();
            t.AintArray.Value = new int[] { 1, 2, 3 };
            t.Update();
            t.AintArray.Value = new int[] { 1, 2, 3 };
            t.Update();
            Assert.AreEqual(1, aIntCnt);
            Assert.AreEqual(2, aIntArrCnt);
        }
        [Test]
        public void TestSimple()
        {
            var t = new T();
            Assert.AreEqual(4, t.Params.Keys.Count);
            Assert.AreEqual(0, t.Aint.Value);
            t.Aint.Value = 1;
            Assert.AreEqual(1, t.Aint.Value);
            Assert.AreEqual(0, t.Aint.LastValue);
            Assert.IsTrue(t.Aint.HasChanged);
            Assert.IsTrue(t.HasChanged);
            t.Commit();
            Assert.AreEqual(1, t.Aint.Value);
            Assert.AreEqual(1, t.Aint.LastValue);
            Assert.IsFalse(t.Aint.HasChanged);
            Assert.IsFalse(t.HasChanged);
            t.Afloat.Value = 77f;
            Assert.AreEqual(1, t.Aint.Value);
            Assert.AreEqual(77f, t.Afloat.Value);
            Assert.AreEqual(0, t.Afloat.LastValue);
            Assert.IsFalse(t.Aint.HasChanged);
            Assert.IsTrue(t.Afloat.HasChanged);
            Assert.IsTrue(t.HasChanged);
            t.Rollback();
            Assert.AreEqual(0f, t.Afloat.Value);
            Assert.IsFalse(t.Afloat.HasChanged);
            Assert.IsFalse(t.HasChanged);
        }
    }
}